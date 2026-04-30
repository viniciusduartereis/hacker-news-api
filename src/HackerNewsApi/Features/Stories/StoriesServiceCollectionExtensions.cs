using FluentValidation;
using HackerNewsApi.ExternalServices.HackerNews;
using HackerNewsApi.Features.Stories.BackgroundServices;
using HackerNewsApi.Features.Stories.Mapping;
using HackerNewsApi.Features.Stories.Providers;
using HackerNewsApi.Features.Stories.Services;
using HackerNewsApi.Features.Stories.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace HackerNewsApi.Features.Stories;

public static class StoriesServiceCollectionExtensions
{
    public static IServiceCollection AddStoriesFeature(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<GetBestStoriesRequestValidator>();

        services.AddSingleton<IHackerNewsStoryMapper, HackerNewsStoryMapper>();
        services.AddScoped<IHackerNewsClient, HackerNewsClient>();
        services.AddScoped<IHackerNewsStoryProvider, CachedHackerNewsStoryProvider>();
        services.AddScoped<IHackerNewsService, HackerNewsService>();
        services.AddScoped<IHackerNewsCacheWarmer, HackerNewsCacheWarmer>();
        services.AddHostedService<HackerNewsCacheRefreshService>();

        return services;
    }
}
