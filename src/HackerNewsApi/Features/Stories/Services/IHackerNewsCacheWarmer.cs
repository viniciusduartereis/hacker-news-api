namespace HackerNewsApi.Features.Stories.Services;

public interface IHackerNewsCacheWarmer
{
    Task WarmUpAsync(int storyCount, CancellationToken ct = default);
}
