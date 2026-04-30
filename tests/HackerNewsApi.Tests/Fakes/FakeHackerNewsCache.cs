using System.Collections.Concurrent;
using HackerNewsApi.Features.Stories.Caching;

namespace HackerNewsApi.Tests.Fakes;

internal sealed class FakeHackerNewsCache : IHackerNewsCache
{
    private readonly ConcurrentDictionary<string, object> _values = new();

    public IReadOnlyCollection<string> Keys => _values.Keys.ToArray();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(
            _values.TryGetValue(key, out var value) && value is T typedValue
                ? typedValue
                : default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _values[key] = value!;

        return Task.CompletedTask;
    }
}
