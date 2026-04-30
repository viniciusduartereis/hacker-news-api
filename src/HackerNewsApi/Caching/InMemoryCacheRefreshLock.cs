namespace HackerNewsApi.Caching;

public sealed class InMemoryCacheRefreshLock : ICacheRefreshLock
{
    private static readonly Dictionary<string, SemaphoreSlim> Locks = new();
    private static readonly object SyncRoot = new();

    public async Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var lockHandle = GetLock(key);
        var acquired = await lockHandle.WaitAsync(0, ct);

        return acquired
            ? new Lease(lockHandle)
            : null;
    }

    private static SemaphoreSlim GetLock(string key)
    {
        lock (SyncRoot)
        {
            if (!Locks.TryGetValue(key, out var lockHandle))
            {
                lockHandle = new SemaphoreSlim(1, 1);
                Locks[key] = lockHandle;
            }

            return lockHandle;
        }
    }

    private sealed class Lease : IAsyncDisposable
    {
        private readonly SemaphoreSlim _lockHandle;

        public Lease(SemaphoreSlim lockHandle)
        {
            _lockHandle = lockHandle;
        }

        public ValueTask DisposeAsync()
        {
            _lockHandle.Release();
            return ValueTask.CompletedTask;
        }
    }
}
