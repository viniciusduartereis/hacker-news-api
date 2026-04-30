using HackerNewsApi.Features.Stories.Contracts;
using HackerNewsApi.Features.Stories.Services;

namespace HackerNewsApi.Tests.Fakes;

internal sealed class FakeStoriesService : IHackerNewsService
{
    private static readonly StoryDto[] Stories =
    [
        new(
            Title: "Story 1",
            Uri: "https://example.com/story-1",
            PostedBy: "tester",
            Time: "2026-04-28T12:00:00+00:00",
            Score: 99,
            CommentCount: 1),
        new(
            Title: "Story 2",
            Uri: "https://example.com/story-2",
            PostedBy: "tester",
            Time: "2026-04-28T12:00:00+00:00",
            Score: 98,
            CommentCount: 2),
        new(
            Title: "Story 3",
            Uri: "https://example.com/story-3",
            PostedBy: "tester",
            Time: "2026-04-28T12:00:00+00:00",
            Score: 97,
            CommentCount: 3)
    ];

    public Task<IReadOnlyList<StoryDto>> GetBestStoriesAsync(int count, CancellationToken ct = default)
    {
        IReadOnlyList<StoryDto> stories = Stories.Take(count).ToArray();

        return Task.FromResult(stories);
    }

    public Task<PagedStoriesResponse> GetStoriesAsync(
        HackerNewsStoryFeed feed,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var items = Stories
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        var response = new PagedStoriesResponse(
            items,
            page,
            pageSize,
            Stories.Length,
            (int)Math.Ceiling(Stories.Length / (double)pageSize));

        return Task.FromResult(response);
    }
}
