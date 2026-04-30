using HackerNewsApi.Caching;
using HackerNewsApi.ExternalServices.HackerNews;
using HackerNewsApi.Features.Stories.Contracts;
using HackerNewsApi.Features.Stories.Mapping;
using HackerNewsApi.Observability;
using HackerNewsApi.Features.Stories.Settings;
using Microsoft.Extensions.Options;

namespace HackerNewsApi.Features.Stories.Providers;

public sealed class CachedHackerNewsStoryProvider : IHackerNewsStoryProvider, IDisposable
{
    private const string BestStoryIdsCacheKey = "hackernews:best-story-ids";
    private const string TopStoryIdsCacheKey = "hackernews:top-story-ids";
    private const string NewStoryIdsCacheKey = "hackernews:new-story-ids";

    private readonly IHackerNewsClient _client;
    private readonly IHackerNewsCache _cache;
    private readonly ICacheRefreshLock _cacheLock;
    private readonly IHackerNewsStoryMapper _mapper;
    private readonly HackerNewsSettings _settings;
    private readonly ILogger<CachedHackerNewsStoryProvider> _logger;
    private readonly SemaphoreSlim _fetchSemaphore;

    public CachedHackerNewsStoryProvider(
        IHackerNewsClient client,
        IHackerNewsCache cache,
        ICacheRefreshLock cacheLock,
        IHackerNewsStoryMapper mapper,
        IOptions<HackerNewsSettings> settings,
        ILogger<CachedHackerNewsStoryProvider> logger)
    {
        _client = client;
        _cache = cache;
        _cacheLock = cacheLock;
        _mapper = mapper;
        _settings = settings.Value;
        _logger = logger;

        var fetchConcurrency = Math.Max(1, _settings.FetchConcurrency);
        _fetchSemaphore = new SemaphoreSlim(fetchConcurrency, fetchConcurrency);
    }

    public async Task<int[]> GetStoryIdsAsync(
        HackerNewsStoryFeed feed,
        bool forceRefresh = false,
        CancellationToken ct = default)
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
        _logger.LogInformation("Cache miss or refresh requested - fetching {Feed} story IDs from HN API", feed);

        var ids = await _client.GetStoryIdsAsync(feed, ct);

        await _cache.SetAsync(cacheKey, ids, _settings.BestStoriesCacheDuration, ct);
        activity?.SetTag("hackernews.feed_ids.count", ids.Length);
        return ids;
    }

    public async Task<StoryDto?> GetStoryAsync(
        int id,
        bool forceRefresh = false,
        CancellationToken ct = default)
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

            var item = await _client.GetStoryAsync(id, ct);

            if (item is null)
                return null;

            var dto = _mapper.Map(item);
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

    private async Task<StoryDto?> WaitForCachedStoryAsync(string cacheKey, CancellationToken ct)
    {
        var timeout = TimeProvider.System.GetTimestamp()
            + (long)(_settings.StorySingleFlightWaitDuration.TotalSeconds * TimeProvider.System.TimestampFrequency);

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

    public void Dispose()
    {
        _fetchSemaphore.Dispose();
    }
}
