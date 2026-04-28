using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HackerNewsApi.Caching;
using HackerNewsApi.Configurations;
using HackerNewsApi.Contracts;
using HackerNewsApi.Observability;
using HackerNewsApi.Settings;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;

namespace HackerNewsApi.Services;

/// <summary>
/// Fetches and caches data from the Hacker News Firebase API.
///
/// Caching strategy:
///   1. Feed ID lists are cached for the configured TTL.
///   2. Each individual story is cached separately for the configured TTL.
///
/// This ensures the downstream HN API is not hammered under high request volumes
/// while keeping data reasonably fresh.
/// </summary>
public sealed class HackerNewsService : IHackerNewsService, IHackerNewsCacheWarmer, IDisposable
{
    private const string BestStoryIdsCacheKey = "hackernews:best-story-ids";
    private const string TopStoryIdsCacheKey = "hackernews:top-story-ids";
    private const string NewStoryIdsCacheKey = "hackernews:new-story-ids";
    private const int MaxBestStories = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _http;
    private readonly IHackerNewsCache _cache;
    private readonly ICacheRefreshLock _cacheLock;
    private readonly HackerNewsSettings _settings;
    private readonly ILogger<HackerNewsService> _logger;
    private readonly SemaphoreSlim _fetchSemaphore;
    private readonly ResiliencePipeline<HttpResponseMessage> _httpReadPipeline;

    public HackerNewsService(
        IHttpClientFactory httpClientFactory,
        IHackerNewsCache cache,
        ICacheRefreshLock cacheLock,
        ResiliencePipelineProvider<string> pipelineProvider,
        IOptions<HackerNewsSettings> settings,
        ILogger<HackerNewsService> logger)
    {
        _settings = settings.Value;
        _http = httpClientFactory.CreateClient(HttpClientConfiguration.HackerNewsClientName);
        _cache = cache;
        _cacheLock = cacheLock;
        _logger = logger;
        _httpReadPipeline = pipelineProvider.GetPipeline<HttpResponseMessage>(HttpResiliencePipelineKeys.SafeRead);
        var fetchConcurrency = Math.Max(1, _settings.FetchConcurrency);
        _fetchSemaphore = new SemaphoreSlim(fetchConcurrency, fetchConcurrency);
    }

    public async Task<IReadOnlyList<StoryDto>> GetBestStoriesAsync(int count, CancellationToken ct = default)
    {
        var ids = await GetStoryIdsAsync(HackerNewsStoryFeed.BestStories, ct);
        var tasks = ids.Select(id => GetStoryAsync(id, ct));
        var allStories = await Task.WhenAll(tasks);

        return allStories
            .Where(s => s is not null)
            .OrderByDescending(s => s!.Score)
            .Take(count)
            .Select(s => s!)
            .ToList()
            .AsReadOnly();
    }

    public async Task<PagedStoriesResponse> GetStoriesAsync(
        HackerNewsStoryFeed feed,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var ids = await GetStoryIdsAsync(feed, ct);
        var totalItems = ids.Length;
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        var skip = (long)(page - 1) * pageSize;
        var pageIds = skip >= totalItems
            ? Array.Empty<int>()
            : ids.Skip((int)skip).Take(pageSize).ToArray();

        var tasks = pageIds.Select(id => GetStoryAsync(id, ct));
        var pageStories = await Task.WhenAll(tasks);

        var items = pageStories
            .Where(story => story is not null)
            .OrderByDescending(story => story!.Score)
            .Select(story => story!)
            .ToList()
            .AsReadOnly();

        return new PagedStoriesResponse(items, page, pageSize, totalItems, totalPages);
    }

    public async Task WarmUpAsync(int storyCount, CancellationToken ct = default)
    {
        var requestedCount = Math.Clamp(storyCount, 0, MaxBestStories);
        await WarmUpFeedAsync(HackerNewsStoryFeed.BestStories, requestedCount, ct);
        await WarmUpFeedAsync(HackerNewsStoryFeed.TopStories, requestedCount, ct);
        await WarmUpFeedAsync(HackerNewsStoryFeed.NewStories, requestedCount, ct);
    }

    private async Task WarmUpFeedAsync(HackerNewsStoryFeed feed, int requestedCount, CancellationToken ct)
    {
        var ids = await GetStoryIdsAsync(feed, ct, forceRefresh: true);
        var idsToWarm = ids.Take(requestedCount).ToArray();

        if (idsToWarm.Length == 0)
            return;

        var tasks = idsToWarm.Select(id => GetStoryAsync(id, ct, forceRefresh: true));
        var stories = await Task.WhenAll(tasks);
        var refreshedCount = stories.Count(story => story is not null);

        _logger.LogInformation(
            "Refreshed {RefreshedCount}/{RequestedCount} Hacker News {Feed} stories in cache",
            refreshedCount,
            idsToWarm.Length,
            feed);
    }

    private async Task<int[]> GetStoryIdsAsync(
        HackerNewsStoryFeed feed,
        CancellationToken ct,
        bool forceRefresh = false)
    {
        using var activity = HackerNewsTelemetry.ActivitySource.StartActivity("hackernews.feed_ids.get");
        activity?.SetTag("hackernews.feed", feed.ToString());
        activity?.SetTag("hackernews.force_refresh", forceRefresh);

        var cacheKey = GetStoryIdsCacheKey(feed);

        if (!forceRefresh)
        {
            var cached = await _cache.GetAsync<int[]>(cacheKey, ct);
            if (cached is not null)
            {
                activity?.SetTag("cache.hit", true);
                activity?.SetTag("hackernews.feed_ids.count", cached.Length);
                return cached;
            }
        }

        activity?.SetTag("cache.hit", false);
        var path = GetStoryIdsPath(feed);
        _logger.LogInformation("Cache miss or refresh requested - fetching {Feed} story IDs from HN API", feed);

        var ids = await GetFromHackerNewsAsync<int[]>(path, ct)
                  ?? Array.Empty<int>();

        await _cache.SetAsync(cacheKey, ids, _settings.BestStoriesCacheDuration, ct);
        activity?.SetTag("hackernews.feed_ids.count", ids.Length);
        return ids;
    }

    private async Task<StoryDto?> GetStoryAsync(int id, CancellationToken ct, bool forceRefresh = false)
    {
        using var activity = HackerNewsTelemetry.ActivitySource.StartActivity("hackernews.story.get");
        activity?.SetTag("hackernews.story.id", id);
        activity?.SetTag("hackernews.force_refresh", forceRefresh);

        var cacheKey = GetStoryCacheKey(id);

        if (!forceRefresh)
        {
            var cached = await _cache.GetAsync<StoryDto>(cacheKey, ct);
            if (cached is not null)
            {
                activity?.SetTag("cache.hit", true);
                return cached;
            }
        }

        activity?.SetTag("cache.hit", false);

        IAsyncDisposable? singleFlightLease = null;

        if (!forceRefresh)
        {
            singleFlightLease = await _cacheLock.TryAcquireAsync(
                GetStorySingleFlightLockKey(id),
                _settings.StorySingleFlightLockDuration,
                ct);

            activity?.SetTag("hackernews.story.single_flight_lock_acquired", singleFlightLease is not null);

            if (singleFlightLease is null)
            {
                var populatedByOtherRequest = await WaitForCachedStoryAsync(cacheKey, ct);
                if (populatedByOtherRequest is not null)
                {
                    activity?.SetTag("hackernews.story.single_flight_wait_hit", true);
                    return populatedByOtherRequest;
                }

                activity?.SetTag("hackernews.story.single_flight_wait_hit", false);
            }
        }

        var semaphoreAcquired = false;

        try
        {
            await _fetchSemaphore.WaitAsync(ct);
            semaphoreAcquired = true;

            if (!forceRefresh)
            {
                var cached = await _cache.GetAsync<StoryDto>(cacheKey, ct);
                if (cached is not null)
                {
                    activity?.SetTag("cache.hit_after_lock", true);
                    return cached;
                }
            }

            var item = await GetFromHackerNewsAsync<HackerNewsItem>($"item/{id}.json", ct);

            if (item is null)
                return null;

            var dto = MapToDto(item);
            await _cache.SetAsync(cacheKey, dto, _settings.StoryCacheDuration, ct);
            return dto;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(ex, "Failed to fetch story {Id}", id);
            return null;
        }
        finally
        {
            if (semaphoreAcquired)
            {
                _fetchSemaphore.Release();
            }

            if (singleFlightLease is not null)
            {
                await singleFlightLease.DisposeAsync();
            }
        }
    }

    private static string GetStoryCacheKey(int id) => $"hackernews:story:{id}";

    private static string GetStorySingleFlightLockKey(int id) => $"hackernews:story:{id}:single-flight";

    private static string GetStoryIdsCacheKey(HackerNewsStoryFeed feed) => feed switch
    {
        HackerNewsStoryFeed.BestStories => BestStoryIdsCacheKey,
        HackerNewsStoryFeed.TopStories => TopStoryIdsCacheKey,
        HackerNewsStoryFeed.NewStories => NewStoryIdsCacheKey,
        _ => throw new ArgumentOutOfRangeException(nameof(feed), feed, "Unsupported Hacker News story feed.")
    };

    private static string GetStoryIdsPath(HackerNewsStoryFeed feed) => feed switch
    {
        HackerNewsStoryFeed.BestStories => "beststories.json",
        HackerNewsStoryFeed.TopStories => "topstories.json",
        HackerNewsStoryFeed.NewStories => "newstories.json",
        _ => throw new ArgumentOutOfRangeException(nameof(feed), feed, "Unsupported Hacker News story feed.")
    };

    private async Task<T?> GetFromHackerNewsAsync<T>(string path, CancellationToken ct)
    {
        using var activity = HackerNewsTelemetry.ActivitySource.StartActivity("hackernews.http.get");
        activity?.SetTag("http.request.method", "GET");
        activity?.SetTag("url.path", path);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path.TrimStart('/'));
            using var response = await _httpReadPipeline.SendAsync(_http, request, ct);

            activity?.SetTag("http.response.status_code", (int)response.StatusCode);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task<StoryDto?> WaitForCachedStoryAsync(string cacheKey, CancellationToken ct)
    {
        var timeout = TimeProvider.System.GetTimestamp() + (long)(_settings.StorySingleFlightWaitDuration.TotalSeconds * TimeProvider.System.TimestampFrequency);

        while (TimeProvider.System.GetTimestamp() < timeout)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);

            var cached = await _cache.GetAsync<StoryDto>(cacheKey, ct);
            if (cached is not null)
            {
                return cached;
            }
        }

        return null;
    }

    private static StoryDto MapToDto(HackerNewsItem item)
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(item.Time)
                                 .ToString("yyyy-MM-dd'T'HH:mm:sszzz");

        return new StoryDto(
            Title: item.Title ?? string.Empty,
            Uri: item.Url,
            PostedBy: item.By ?? string.Empty,
            Time: time,
            Score: item.Score,
            CommentCount: item.Descendants
        );
    }

    public void Dispose()
    {
        _fetchSemaphore.Dispose();
    }
}
