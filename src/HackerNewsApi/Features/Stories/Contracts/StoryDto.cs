namespace HackerNewsApi.Features.Stories.Contracts;

/// <summary>
/// Outbound DTO returned to API callers.
/// </summary>
public sealed record StoryDto(
    string Title,
    string? Uri,
    string PostedBy,
    string Time,
    int Score,
    int CommentCount
);
