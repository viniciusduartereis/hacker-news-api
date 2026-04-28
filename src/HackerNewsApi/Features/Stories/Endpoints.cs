using HackerNewsApi.Contracts;
using HackerNewsApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;


namespace HackerNewsApi.Features.Stories;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapStoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/stories")
                       .WithTags("Stories")
                       .RequireRateLimiting("FixedWindow");

        group.MapGet("/{count:int}", GetBestStoriesAsync)
             .WithName("GetBestStories")
             .WithSummary("Returns the best n Hacker News stories ordered by score descending.")
             .Produces(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/{feed}", GetPagedStoriesAsync)
             .WithName("GetPagedStories")
             .WithSummary("Returns a paged Hacker News story feed.")
             .Produces<PagedStoriesResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetBestStoriesAsync(
        [FromRoute] int count,
        [FromServices] IHackerNewsService service,
        CancellationToken ct)
    {
        if (count <= 0)
            return Results.BadRequest("count must be a positive integer.");

        if (count > 500)
            return Results.BadRequest("count cannot exceed 500 (maximum returned by the HN best-stories endpoint).");

        var stories = await service.GetBestStoriesAsync(count, ct);
        return Results.Ok(stories);
    }

    private static async Task<IResult> GetPagedStoriesAsync(
        [FromRoute] string feed,
        [FromServices] IHackerNewsService service,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!TryParseFeed(feed, out var storyFeed))
            return Results.BadRequest("feed must be one of: best, beststories, top, topstories, new, newstories.");

        if (page < 1)
            return Results.BadRequest("page must be greater than or equal to 1.");

        if (pageSize < 1)
            return Results.BadRequest("pageSize must be greater than or equal to 1.");

        if (pageSize > 500)
            return Results.BadRequest("pageSize cannot exceed 500.");

        var response = await service.GetStoriesAsync(storyFeed, page, pageSize, ct);
        return Results.Ok(response);
    }

    private static bool TryParseFeed(string feed, out HackerNewsStoryFeed storyFeed)
    {
        var normalizedFeed = feed.Trim().ToLowerInvariant();

        storyFeed = normalizedFeed switch
        {
            "best" or "beststories" => HackerNewsStoryFeed.BestStories,
            "top" or "topstories" => HackerNewsStoryFeed.TopStories,
            "new" or "newstories" => HackerNewsStoryFeed.NewStories,
            _ => default
        };

        return normalizedFeed is "best" or "beststories" or "top" or "topstories" or "new" or "newstories";
    }
}
