namespace HackerNewsApi.Features.Stories.Caching;

public interface IHackerNewsCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
}
