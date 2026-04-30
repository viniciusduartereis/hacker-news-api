using HackerNewsApi.Features.Stories.Contracts;

namespace HackerNewsApi.Features.Stories.Providers;

public interface IHackerNewsStoryProvider
{
    Task<int[]> GetStoryIdsAsync(
        HackerNewsStoryFeed feed,
        bool forceRefresh = false,
        CancellationToken ct = default);

    Task<StoryDto?> GetStoryAsync(
        int id,
        bool forceRefresh = false,
        CancellationToken ct = default);
}
