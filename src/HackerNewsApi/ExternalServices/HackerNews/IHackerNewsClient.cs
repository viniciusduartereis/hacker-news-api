using HackerNewsApi.ExternalServices.HackerNews.Contracts;
using HackerNewsApi.Features.Stories.Contracts;

namespace HackerNewsApi.ExternalServices.HackerNews;

public interface IHackerNewsClient
{
    Task<int[]> GetStoryIdsAsync(HackerNewsStoryFeed feed, CancellationToken ct = default);

    Task<HackerNewsItem?> GetStoryAsync(int id, CancellationToken ct = default);
}
