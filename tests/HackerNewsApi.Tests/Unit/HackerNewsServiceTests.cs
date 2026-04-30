using FluentAssertions;
using HackerNewsApi.Caching;
using HackerNewsApi.Configurations;
using HackerNewsApi.ExternalServices.HackerNews;
using HackerNewsApi.ExternalServices.HackerNews.Contracts;
using HackerNewsApi.Features.Stories.Caching;
using HackerNewsApi.Features.Stories.Contracts;
using HackerNewsApi.Features.Stories.Mapping;
using HackerNewsApi.Features.Stories.Providers;
using HackerNewsApi.Features.Stories.Services;
using HackerNewsApi.Features.Stories.Settings;
using HackerNewsApi.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.Registry;

namespace HackerNewsApi.Tests.Unit;

public sealed class HackerNewsServiceTests
{
    [Fact]
    public async Task GetBestStoriesAsync_ReturnsStoriesRankedByScoreDescending()
    {
        var hn = new FakeHackerNewsHandler();
        hn.AddJsonResponse("beststories.json", new[] { 10, 20, 30 });
        hn.AddJsonResponse("item/10.json", Item(10, score: 50));
        hn.AddJsonResponse("item/20.json", Item(20, score: 150));
        hn.AddJsonResponse("item/30.json", Item(30, score: 100));

        await using var fixture = CreateService(hn);

        var stories = await fixture.Service.GetBestStoriesAsync(3);

        stories.Select(story => story.Score).Should().Equal(150, 100, 50);
        stories.Select(story => story.Title).Should().Equal("Story 20", "Story 30", "Story 10");
    }

    [Fact]
    public async Task GetBestStoriesAsync_UsesCachedStoryIdsAndDetailsWhenAvailable()
    {
        var cachedStory = new StoryDto(
            Title: "Cached story",
            Uri: "https://example.com/cached",
            PostedBy: "cache",
            Time: "2026-04-28T12:00:00+00:00",
            Score: 999,
            CommentCount: 12);

        var cache = new FakeHackerNewsCache();
        await cache.SetAsync("hackernews:best-story-ids", new[] { 42 }, TimeSpan.FromMinutes(5));
        await cache.SetAsync("hackernews:story:42", cachedStory, TimeSpan.FromMinutes(5));

        var hn = new FakeHackerNewsHandler();
        await using var fixture = CreateService(hn, cache);

        var stories = await fixture.Service.GetBestStoriesAsync(1);

        stories.Should().ContainSingle().Which.Should().BeEquivalentTo(cachedStory);
        hn.RequestCount("beststories.json").Should().Be(0);
        hn.RequestCount("item/42.json").Should().Be(0);
    }

    [Fact]
    public async Task GetBestStoriesAsync_CachesFetchedStoryIdsAndDetails()
    {
        var hn = new FakeHackerNewsHandler();
        hn.AddJsonResponse("beststories.json", new[] { 7 });
        hn.AddJsonResponse("item/7.json", Item(7, score: 77));

        var cache = new FakeHackerNewsCache();
        await using var fixture = CreateService(hn, cache);

        var stories = await fixture.Service.GetBestStoriesAsync(1);

        stories.Should().ContainSingle(story => story.Title == "Story 7");
        cache.Keys.Should().BeEquivalentTo("hackernews:best-story-ids", "hackernews:story:7");
    }

    [Fact]
    public async Task GetStoriesAsync_ReturnsPaginationMetadataWithTotalItemsAndTotalPages()
    {
        var hn = new FakeHackerNewsHandler();
        hn.AddJsonResponse("newstories.json", new[] { 10, 20, 30, 40, 50 });
        hn.AddJsonResponse("item/30.json", Item(30, score: 300));
        hn.AddJsonResponse("item/40.json", Item(40, score: 400));

        await using var fixture = CreateService(hn);

        var page = await fixture.Service.GetStoriesAsync(HackerNewsStoryFeed.NewStories, page: 2, pageSize: 2);

        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);
        page.TotalItems.Should().Be(5);
        page.TotalPages.Should().Be(3);
        page.Items.Select(story => story.Score).Should().Equal(400, 300);
        hn.RequestCount("newstories.json").Should().Be(1);
        hn.RequestCount("item/10.json").Should().Be(0);
        hn.RequestCount("item/20.json").Should().Be(0);
        hn.RequestCount("item/30.json").Should().Be(1);
        hn.RequestCount("item/40.json").Should().Be(1);
        hn.RequestCount("item/50.json").Should().Be(0);
    }

    private static ServiceFixture CreateService(
        FakeHackerNewsHandler hn,
        IHackerNewsCache? cache = null,
        HackerNewsSettings? settings = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationHttpResiliencePipelines();
        services.AddHttpClient(HttpClientConfiguration.HackerNewsClientName, client =>
        {
            client.BaseAddress = new Uri("https://hacker-news.test/v0/");
        })
        .ConfigurePrimaryHttpMessageHandler(() => hn);

        var serviceProvider = services.BuildServiceProvider();
        var client = new HackerNewsClient(
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            serviceProvider.GetRequiredService<ResiliencePipelineProvider<string>>());
        var storyProvider = new CachedHackerNewsStoryProvider(
            client,
            cache ?? new FakeHackerNewsCache(),
            new InMemoryCacheRefreshLock(),
            new HackerNewsStoryMapper(),
            Options.Create(settings ?? new HackerNewsSettings { FetchConcurrency = 4 }),
            NullLogger<CachedHackerNewsStoryProvider>.Instance);
        var service = new HackerNewsService(storyProvider);

        return new ServiceFixture(serviceProvider, service, storyProvider);
    }

    private static HackerNewsItem Item(int id, int score)
    {
        return new HackerNewsItem(
            Id: id,
            Title: $"Story {id}",
            Url: $"https://example.com/{id}",
            By: "author",
            Time: 1_775_000_000,
            Score: score,
            Descendants: id);
    }

    private sealed class ServiceFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly CachedHackerNewsStoryProvider _storyProvider;

        public ServiceFixture(
            ServiceProvider provider,
            HackerNewsService service,
            CachedHackerNewsStoryProvider storyProvider)
        {
            _provider = provider;
            Service = service;
            _storyProvider = storyProvider;
        }

        public HackerNewsService Service { get; }

        public async ValueTask DisposeAsync()
        {
            _storyProvider.Dispose();
            await _provider.DisposeAsync();
        }
    }
}
