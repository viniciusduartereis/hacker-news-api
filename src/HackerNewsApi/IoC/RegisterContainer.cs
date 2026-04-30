using HackerNewsApi.Caching;
using HackerNewsApi.Configurations;
using HackerNewsApi.Features.Stories;
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

        services.AddStoriesFeature(configuration);
    }
}
