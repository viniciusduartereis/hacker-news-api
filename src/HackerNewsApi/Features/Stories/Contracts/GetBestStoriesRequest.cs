using Microsoft.AspNetCore.Mvc;

namespace HackerNewsApi.Features.Stories.Contracts;

public sealed class GetBestStoriesRequest
{
    [FromRoute(Name = "count")]
    public int Count { get; set; }
}
