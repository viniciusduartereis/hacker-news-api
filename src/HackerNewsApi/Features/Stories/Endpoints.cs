using FluentValidation;
using FluentValidation.Results;
using HackerNewsApi.Features.Stories.Contracts;
using HackerNewsApi.Features.Stories.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;


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
        [AsParameters] GetBestStoriesRequest request,
        [FromServices] IHackerNewsService service,
        [FromServices] IValidator<GetBestStoriesRequest> validator,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return ToValidationError(validationResult);

        var stories = await service.GetBestStoriesAsync(request.Count, ct);
        return Results.Ok(stories);
    }

    private static async Task<IResult> GetPagedStoriesAsync(
        [AsParameters] GetPagedStoriesRequest request,
        [FromServices] IHackerNewsService service,
        [FromServices] IValidator<GetPagedStoriesRequest> validator,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return ToValidationError(validationResult);

        TryParseFeed(request.Feed, out var storyFeed);

        var response = await service.GetStoriesAsync(storyFeed, request.Page, request.PageSize, ct);
        return Results.Ok(response);
    }

    private static IResult ToValidationError(ValidationResult validationResult)
    {
        var errors = validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        return Results.ValidationProblem(errors);
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
