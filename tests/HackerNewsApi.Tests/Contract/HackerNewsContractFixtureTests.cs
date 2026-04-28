using System.Text.Json;
using FluentAssertions;
using HackerNewsApi.Contracts;

namespace HackerNewsApi.Tests.Contract;

public sealed class HackerNewsContractFixtureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Theory]
    [InlineData("beststories.json")]
    [InlineData("topstories.json")]
    [InlineData("newstories.json")]
    public async Task StoryIdListFixtures_MatchHackerNewsContract(string fixtureName)
    {
        var ids = await ReadFixtureAsync<int[]>(fixtureName);

        ids.Should().NotBeNull();
        ids.Should().NotBeEmpty();
        ids.Should().OnlyContain(id => id > 0);
    }

    [Theory]
    [InlineData("item-21233041.json")]
    [InlineData("item-31233041.json")]
    [InlineData("item-41233041.json")]
    public async Task ItemFixtures_MatchHackerNewsContract(string fixtureName)
    {
        var item = await ReadFixtureAsync<HackerNewsItem>(fixtureName);

        item.Should().NotBeNull();
        item.Id.Should().BeGreaterThan(0);
        item.Title.Should().NotBeNullOrWhiteSpace();
        item.By.Should().NotBeNullOrWhiteSpace();
        item.Time.Should().BeGreaterThan(0);
        item.Score.Should().BeGreaterThanOrEqualTo(0);
        item.Descendants.Should().BeGreaterThanOrEqualTo(0);
    }

    private static async Task<T> ReadFixtureAsync<T>(string fixtureName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "HackerNews", fixtureName);
        await using var stream = File.OpenRead(path);

        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);

        return value ?? throw new InvalidOperationException($"Fixture {fixtureName} could not be deserialized.");
    }
}
