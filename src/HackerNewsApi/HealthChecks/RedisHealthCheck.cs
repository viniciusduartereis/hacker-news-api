using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace HackerNewsApi.HealthChecks;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public RedisHealthCheck(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("RedisConnectionString") ?? string.Empty;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return HealthCheckResult.Healthy("Redis is not configured.");
        }

        try
        {
            using var redis = await ConnectionMultiplexer.ConnectAsync(CreateRedisOptions(_connectionString));
            var latency = await redis.GetDatabase().PingAsync();

            return HealthCheckResult.Healthy("Redis is reachable.", new Dictionary<string, object>
            {
                ["latencyMs"] = latency.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis is not reachable.", ex);
        }
    }

    private static ConfigurationOptions CreateRedisOptions(string connectionString)
    {
        var redisOptions = ConfigurationOptions.Parse(connectionString);
        redisOptions.AbortOnConnectFail = false;
        redisOptions.ConnectRetry = 1;
        redisOptions.ConnectTimeout = 1_000;
        redisOptions.SyncTimeout = 1_000;

        return redisOptions;
    }
}
