namespace HackerNewsApi.Features.Stories.Contracts;

public sealed record PagedStoriesResponse(
    IReadOnlyList<StoryDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages
);
