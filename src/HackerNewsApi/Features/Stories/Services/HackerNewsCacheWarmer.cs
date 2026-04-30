using HackerNewsApi.Features.Stories.Contracts;
using HackerNewsApi.Features.Stories.Providers;

namespace HackerNewsApi.Features.Stories.Services;

public sealed class HackerNewsCacheWarmer : IHackerNewsCacheWarmer
{
    private const int MaxBestStories = 500;

    private readonly IHackerNewsStoryProvider _storyProvider;
    private readonly ILogger<HackerNewsCacheWarmer> _logger;

    public HackerNewsCacheWarmer(
        IHackerNewsStoryProvider storyProvider,
        ILogger<HackerNewsCacheWarmer> logger)
    {
        _storyProvider = storyProvider;
        _logger = logger;
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
        var ids = await _storyProvider.GetStoryIdsAsync(feed, forceRefresh: true, ct: ct);
        var idsToWarm = ids.Take(requestedCount).ToArray();

        if (idsToWarm.Length == 0)
            return;

        var tasks = idsToWarm.Select(id => _storyProvider.GetStoryAsync(id, forceRefresh: true, ct: ct));
        var stories = await Task.WhenAll(tasks);
        var refreshedCount = stories.Count(story => story is not null);

        _logger.LogInformation(
            "Refreshed {RefreshedCount}/{RequestedCount} Hacker News {Feed} stories in cache",
            refreshedCount,
            idsToWarm.Length,
            feed);
    }
}
