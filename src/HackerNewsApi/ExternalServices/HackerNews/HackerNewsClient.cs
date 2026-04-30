using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HackerNewsApi.Configurations;
using HackerNewsApi.ExternalServices.HackerNews.Contracts;
using HackerNewsApi.Features.Stories.Contracts;
using HackerNewsApi.Observability;
using Polly;
using Polly.Registry;

namespace HackerNewsApi.ExternalServices.HackerNews;

public sealed class HackerNewsClient : IHackerNewsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _http;
    private readonly ResiliencePipeline<HttpResponseMessage> _httpReadPipeline;

    public HackerNewsClient(
        IHttpClientFactory httpClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider)
    {
        _http = httpClientFactory.CreateClient(HttpClientConfiguration.HackerNewsClientName);
        _httpReadPipeline = pipelineProvider.GetPipeline<HttpResponseMessage>(HttpResiliencePipelineKeys.SafeRead);
    }

    public async Task<int[]> GetStoryIdsAsync(HackerNewsStoryFeed feed, CancellationToken ct = default)
    {
        return await GetFromHackerNewsAsync<int[]>(GetStoryIdsPath(feed), ct)
               ?? Array.Empty<int>();
    }

    public Task<HackerNewsItem?> GetStoryAsync(int id, CancellationToken ct = default)
    {
        return GetFromHackerNewsAsync<HackerNewsItem>($"item/{id}.json", ct);
    }

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
}
