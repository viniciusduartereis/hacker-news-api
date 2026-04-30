using Microsoft.AspNetCore.Mvc;

namespace HackerNewsApi.Features.Stories.Contracts;

public sealed class GetPagedStoriesRequest
{
    [FromRoute(Name = "feed")]
    public string Feed { get; set; } = string.Empty;

    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;
}
