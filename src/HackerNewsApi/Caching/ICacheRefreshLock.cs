namespace HackerNewsApi.Caching;

public interface ICacheRefreshLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default);
}
