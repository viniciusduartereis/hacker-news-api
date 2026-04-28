using System.Net;
using System.Text.Json;
using FluentAssertions;
using HackerNewsApi.BackgroundServices;
using HackerNewsApi.Services;
using HackerNewsApi.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HackerNewsApi.Tests.Integration;

public sealed class StoriesEndpointTests
{
    [Fact]
    public async Task GetStoriesFeed_ReturnsOkWithPaginationMetadata()
    {
        await using var factory = new HackerNewsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/stories/newstories?page=1&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("pageSize").GetInt32().Should().Be(2);
        document.RootElement.GetProperty("totalItems").GetInt32().Should().BeGreaterThan(0);
        document.RootElement.GetProperty("totalPages").GetInt32().Should().BeGreaterThan(0);
        document.RootElement.GetProperty("items").GetArrayLength().Should().BeLessThanOrEqualTo(2);
    }

    [Theory]
    [InlineData("/api/v1/stories/beststories?page=0&pageSize=2")]
    [InlineData("/api/v1/stories/beststories?page=1&pageSize=0")]
    [InlineData("/api/v1/stories/beststories?page=1&pageSize=501")]
    public async Task GetStoriesFeed_WithInvalidPagination_ReturnsBadRequest(string url)
    {
        await using var factory = new HackerNewsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetStoriesFeed_WithInvalidFeed_ReturnsBadRequest()
    {
        await using var factory = new HackerNewsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/stories/unsupported?page=1&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetLegacyBestStories_WithPositiveCount_ReturnsOk()
    {
        await using var factory = new HackerNewsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/stories/2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Story 1").And.Contain("Story 2");
    }

    [Theory]
    [InlineData("/api/v1/stories/0")]
    [InlineData("/api/v1/stories/501")]
    public async Task GetLegacyBestStories_WithInvalidCount_ReturnsBadRequest(string url)
    {
        await using var factory = new HackerNewsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed class HackerNewsApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("AppSettings:EnableHttpsRedirection", "false");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IHackerNewsService>();
                services.RemoveAll<HackerNewsService>();
                services.RemoveAll<IHackerNewsCacheWarmer>();
                services.RemoveAll<HackerNewsCacheRefreshService>();
                services.AddScoped<IHackerNewsService, FakeStoriesService>();
            });
        }
    }
}
