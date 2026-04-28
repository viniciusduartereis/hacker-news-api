using HackerNewsApi.BackgroundServices;
using HackerNewsApi.Caching;
using HackerNewsApi.Configurations;
using HackerNewsApi.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace HackerNewsApi.IoC;

public static class RegisterContainer
{
    public static void RegisterDependeciesApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApplicationHttpResiliencePipelines();
        services.AddApplicationCacheResiliencePipelines();

        services.AddSingleton<IHackerNewsCache, DistributedHackerNewsCache>();
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("RedisConnectionString")))
        {
            services.AddSingleton<ICacheRefreshLock, InMemoryCacheRefreshLock>();
        }
        else
        {
            services.AddSingleton<ICacheRefreshLock, RedisCacheRefreshLock>();
        }

        services.AddScoped<HackerNewsService>();
        services.AddScoped<IHackerNewsService>(provider => provider.GetRequiredService<HackerNewsService>());
        services.AddScoped<IHackerNewsCacheWarmer>(provider => provider.GetRequiredService<HackerNewsService>());
        services.AddHostedService<HackerNewsCacheRefreshService>();
    }
}
