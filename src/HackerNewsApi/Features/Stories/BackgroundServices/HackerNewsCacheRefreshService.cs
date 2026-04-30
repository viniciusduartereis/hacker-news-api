using HackerNewsApi.Features.Stories.Caching;
using HackerNewsApi.Features.Stories.Services;
using HackerNewsApi.Features.Stories.Settings;
using HackerNewsApi.Observability;
using Microsoft.Extensions.Options;

namespace HackerNewsApi.Features.Stories.BackgroundServices;

public sealed class HackerNewsCacheRefreshService : BackgroundService
{
    private const string RefreshLockKey = "hackernews:cache-refresh:lock";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICacheRefreshLock _refreshLock;
    private readonly HackerNewsSettings _settings;
    private readonly ILogger<HackerNewsCacheRefreshService> _logger;

    public HackerNewsCacheRefreshService(
        IServiceScopeFactory scopeFactory,
        ICacheRefreshLock refreshLock,
        IOptions<HackerNewsSettings> settings,
        ILogger<HackerNewsCacheRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _refreshLock = refreshLock;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshCacheAsync(stoppingToken);

        using var timer = new PeriodicTimer(_settings.CacheRefreshInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshCacheAsync(stoppingToken);
        }
    }

    private async Task RefreshCacheAsync(CancellationToken ct)
    {
        using var activity = HackerNewsTelemetry.ActivitySource.StartActivity("hackernews.cache_refresh");
        activity?.SetTag("hackernews.cache_refresh.story_count", _settings.CacheWarmupStoryCount);

        try
        {
            await using var refreshLease = await _refreshLock.TryAcquireAsync(
                RefreshLockKey,
                _settings.CacheRefreshLockDuration,
                ct);

            if (refreshLease is null)
            {
                activity?.SetTag("hackernews.cache_refresh.lock_acquired", false);
                _logger.LogInformation("Skipping Hacker News cache refresh because another instance owns the refresh lock");
                return;
            }

            activity?.SetTag("hackernews.cache_refresh.lock_acquired", true);

            using var scope = _scopeFactory.CreateScope();
            var warmer = scope.ServiceProvider.GetRequiredService<IHackerNewsCacheWarmer>();

            _logger.LogInformation("Refreshing Hacker News cache for {StoryCount} stories", _settings.CacheWarmupStoryCount);
            await warmer.WarmUpAsync(_settings.CacheWarmupStoryCount, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown path.
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(ex, "Failed to refresh Hacker News cache");
        }
    }
}
