using System.Text.Json;
using HackerNewsApi.Configurations;
using HackerNewsApi.Observability;
using Microsoft.Extensions.Caching.Distributed;
using Polly;
using Polly.Registry;

namespace HackerNewsApi.Caching;

public sealed class DistributedHackerNewsCache : IHackerNewsCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedHackerNewsCache> _logger;
    private readonly ResiliencePipeline _pipeline;

    public DistributedHackerNewsCache(
        IDistributedCache cache,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<DistributedHackerNewsCache> logger)
    {
        _cache = cache;
        _logger = logger;
        _pipeline = pipelineProvider.GetPipeline(CacheResiliencePipelineKeys.DistributedCache);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        using var activity = HackerNewsTelemetry.ActivitySource.StartActivity("cache.get");
        activity?.SetTag("cache.key", key);

        try
        {
            var json = await _pipeline.ExecuteAsync(
                static async (state, token) => await state.Cache.GetStringAsync(state.Key, token),
                new CacheOperationState(_cache, key),
                ct);

            var hit = !string.IsNullOrWhiteSpace(json);
            activity?.SetTag("cache.hit", hit);

            return hit
                ? JsonSerializer.Deserialize<T>(json!, JsonOptions)
                : default;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(ex, "Cache read failed for key {CacheKey}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        using var activity = HackerNewsTelemetry.ActivitySource.StartActivity("cache.set");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.ttl_ms", ttl.TotalMilliseconds);

        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };

            await _pipeline.ExecuteAsync(
                static async (state, token) =>
                {
                    await state.Cache.SetStringAsync(state.Key, state.Json, state.Options, token);
                },
                new CacheSetOperationState(_cache, key, json, options),
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(ex, "Cache write failed for key {CacheKey}", key);
        }
    }

    private readonly record struct CacheOperationState(IDistributedCache Cache, string Key);

    private readonly record struct CacheSetOperationState(
        IDistributedCache Cache,
        string Key,
        string Json,
        DistributedCacheEntryOptions Options);
}
