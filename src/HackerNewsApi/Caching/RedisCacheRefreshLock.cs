using StackExchange.Redis;

namespace HackerNewsApi.Caching;

public sealed class RedisCacheRefreshLock : ICacheRefreshLock
{
    private readonly string _connectionString;
    private readonly ILogger<RedisCacheRefreshLock> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnectionMultiplexer? _redis;

    public RedisCacheRefreshLock(
        IConfiguration configuration,
        ILogger<RedisCacheRefreshLock> logger)
    {
        _connectionString = configuration.GetConnectionString("RedisConnectionString") ?? string.Empty;
        _logger = logger;
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            var database = await GetDatabaseAsync(ct);
            if (database is null)
            {
                return null;
            }

            var token = $"{Environment.MachineName}:{Guid.NewGuid():N}";
            var acquired = await database.LockTakeAsync(key, token, ttl);

            return acquired
                ? new Lease(database, key, token, _logger)
                : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to acquire distributed cache refresh lock {LockKey}", key);
            return null;
        }
    }

    private async Task<IDatabase?> GetDatabaseAsync(CancellationToken ct)
    {
        if (_redis?.IsConnected == true)
        {
            return _redis.GetDatabase();
        }

        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_redis?.IsConnected == true)
            {
                return _redis.GetDatabase();
            }

            _redis?.Dispose();
            _redis = await ConnectionMultiplexer.ConnectAsync(CreateRedisOptions(_connectionString));

            return _redis.GetDatabase();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Redis for distributed cache refresh lock");
            return null;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private static ConfigurationOptions CreateRedisOptions(string connectionString)
    {
        var redisOptions = ConfigurationOptions.Parse(connectionString);
        redisOptions.AbortOnConnectFail = false;
        redisOptions.ConnectRetry = 3;
        redisOptions.ConnectTimeout = 3_000;
        redisOptions.SyncTimeout = 3_000;

        return redisOptions;
    }

    private sealed class Lease : IAsyncDisposable
    {
        private readonly IDatabase _database;
        private readonly string _key;
        private readonly RedisValue _token;
        private readonly ILogger _logger;

        public Lease(
            IDatabase database,
            string key,
            RedisValue token,
            ILogger logger)
        {
            _database = database;
            _key = key;
            _token = token;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _database.LockReleaseAsync(_key, _token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release distributed cache refresh lock {LockKey}", _key);
            }
        }
    }
}
