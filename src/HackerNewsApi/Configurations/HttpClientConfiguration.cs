using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace HackerNewsApi.Configurations;


public static class HttpClientConfiguration
{
    public const string HackerNewsClientName = "HackerNews";

    public static void ConfigureHttpHackerNewsClient(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration["HackerNews:BaseUrl"] ?? "https://hacker-news.firebaseio.com/v0";
        var normalizedBaseUrl = $"{baseUrl.TrimEnd('/')}/";

        services.AddHttpClient(HackerNewsClientName, client =>
        {
            client.BaseAddress = new Uri(normalizedBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }
}
