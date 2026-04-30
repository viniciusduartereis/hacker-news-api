using HackerNewsApi.Features.Stories.Contracts;

namespace HackerNewsApi.Features.Stories.Services;

public interface IHackerNewsService
{
    /// <summary>
    /// Returns the top <paramref name="count"/> stories ordered by score descending.
    /// </summary>
    Task<IReadOnlyList<StoryDto>> GetBestStoriesAsync(int count, CancellationToken ct = default);

    /// <summary>
    /// Returns a single page from the requested Hacker News story feed.
    /// </summary>
    Task<PagedStoriesResponse> GetStoriesAsync(
        HackerNewsStoryFeed feed,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
