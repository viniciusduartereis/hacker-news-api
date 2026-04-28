namespace HackerNewsApi.Contracts;

/// <summary>
/// Raw item response from the Hacker News API.
/// </summary>
public sealed record HackerNewsItem(
    int Id,
    string? Title,
    string? Url,
    string? By,
    long Time,
    int Score,
    int Descendants
);
