using HackerNewsApi.Features.Stories.Contracts;
using HackerNewsApi.Features.Stories.Providers;

namespace HackerNewsApi.Features.Stories.Services;

/// <summary>
/// Application service responsible for composing Hacker News story use cases.
/// </summary>
public sealed class HackerNewsService : IHackerNewsService
{
    private readonly IHackerNewsStoryProvider _storyProvider;

    public HackerNewsService(IHackerNewsStoryProvider storyProvider)
    {
        _storyProvider = storyProvider;
    }

    public async Task<IReadOnlyList<StoryDto>> GetBestStoriesAsync(int count, CancellationToken ct = default)
    {
        var ids = await _storyProvider.GetStoryIdsAsync(HackerNewsStoryFeed.BestStories, ct: ct);
        var tasks = ids.Select(id => _storyProvider.GetStoryAsync(id, ct: ct));
        var allStories = await Task.WhenAll(tasks);

        return allStories
            .Where(story => story is not null)
            .OrderByDescending(story => story!.Score)
            .Take(count)
            .Select(story => story!)
            .ToList()
            .AsReadOnly();
    }

    public async Task<PagedStoriesResponse> GetStoriesAsync(
        HackerNewsStoryFeed feed,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var ids = await _storyProvider.GetStoryIdsAsync(feed, ct: ct);
        var totalItems = ids.Length;
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        var skip = (long)(page - 1) * pageSize;
        var pageIds = skip >= totalItems
            ? Array.Empty<int>()
            : ids.Skip((int)skip).Take(pageSize).ToArray();

        var tasks = pageIds.Select(id => _storyProvider.GetStoryAsync(id, ct: ct));
        var pageStories = await Task.WhenAll(tasks);

        var items = pageStories
            .Where(story => story is not null)
            .OrderByDescending(story => story!.Score)
            .Select(story => story!)
            .ToList()
            .AsReadOnly();

        return new PagedStoriesResponse(items, page, pageSize, totalItems, totalPages);
    }
}
