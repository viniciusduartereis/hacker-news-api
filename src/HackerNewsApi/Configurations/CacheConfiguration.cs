using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace HackerNewsApi.Configurations;

public static class CacheConfiguration
{
    public static void AddCache(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
       
        var redisConnectionString = configuration.GetConnectionString("RedisConnectionString");

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(options =>
            {
                var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
                redisOptions.AbortOnConnectFail = false;
                redisOptions.ConnectRetry = 3;
                redisOptions.ConnectTimeout = 3_000;
                redisOptions.SyncTimeout = 3_000;

                options.ConfigurationOptions = redisOptions;
                options.InstanceName = "HackerNews:";
            });
        }
    }
}