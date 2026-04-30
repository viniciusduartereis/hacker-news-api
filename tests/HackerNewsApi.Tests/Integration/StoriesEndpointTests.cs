using System.Net;
using System.Text.Json;
using FluentAssertions;
using HackerNewsApi.Features.Stories.BackgroundServices;
using HackerNewsApi.Features.Stories.Services;
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
    [InlineData("/api/v1/stories/beststories?page=0&pageSize=2", "page", "page must be greater than or equal to 1.")]
    [InlineData("/api/v1/stories/beststories?page=1&pageSize=0", "pageSize", "pageSize must be greater than or equal to 1.")]
    [InlineData("/api/v1/stories/beststories?page=1&pageSize=501", "pageSize", "pageSize cannot exceed 500.")]
    public async Task GetStoriesFeed_WithInvalidPagination_ReturnsValidationError(
        string url,
        string propertyName,
        string message)
    {
        await using var factory = new HackerNewsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        await AssertValidationErrorAsync(response, propertyName, message);
    }

    [Fact]
    public async Task GetStoriesFeed_WithInvalidFeed_ReturnsValidationError()
    {
        await using var factory = new HackerNewsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/stories/unsupported?page=1&pageSize=2");

        await AssertValidationErrorAsync(
            response,
            "feed",
            "feed must be one of: best, beststories, top, topstories, new, newstories.");
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
    [InlineData("/api/v1/stories/0", "count must be a positive integer.")]
    [InlineData("/api/v1/stories/501", "count cannot exceed 500 (maximum returned by the HN best-stories endpoint).")]
    public async Task GetLegacyBestStories_WithInvalidCount_ReturnsValidationError(string url, string message)
    {
        await using var factory = new HackerNewsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertValidationErrorAsync(response, "count", message);
    }

    private static async Task AssertValidationErrorAsync(
        HttpResponseMessage response,
        string propertyName,
        string message)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("status").GetInt32().Should().Be(400);
        root.GetProperty("title").GetString().Should().Be("One or more validation errors occurred.");

        root.GetProperty("errors")
            .GetProperty(propertyName)
            .EnumerateArray()
            .Select(item => item.GetString())
            .Should()
            .Contain(message);
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
