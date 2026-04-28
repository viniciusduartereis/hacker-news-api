using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

namespace HackerNewsApi.Configurations;

public static class VersioningConfiguration
{
    public static void AddVersioning(this IServiceCollection services)
    {
        services.AddOpenApi("v1");

        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });
    }

    public static RouteGroupBuilder SetupVersioning(this WebApplication app)
    {
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build();

        RouteGroupBuilder versionedApi = app
            .MapGroup("/api/v{version:apiVersion}")
            .WithGroupName("v1")
            .WithApiVersionSet(apiVersionSet)
            .HasApiVersion(new ApiVersion(1, 0));

        return versionedApi;
    }
}
