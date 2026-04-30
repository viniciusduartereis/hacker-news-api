using FluentValidation;
using HackerNewsApi.Features.Stories.Contracts;

namespace HackerNewsApi.Features.Stories.Validators;

public sealed class GetPagedStoriesRequestValidator : AbstractValidator<GetPagedStoriesRequest>
{
    private static readonly string[] SupportedFeeds =
    [
        "best",
        "beststories",
        "top",
        "topstories",
        "new",
        "newstories"
    ];

    public GetPagedStoriesRequestValidator()
    {
        RuleFor(request => request.Feed)
            .NotEmpty()
            .WithMessage("feed must be one of: best, beststories, top, topstories, new, newstories.")
            .OverridePropertyName("feed")
            .Must(feed => SupportedFeeds.Contains(feed.Trim().ToLowerInvariant()))
            .WithMessage("feed must be one of: best, beststories, top, topstories, new, newstories.")
            .OverridePropertyName("feed");

        RuleFor(request => request.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("page must be greater than or equal to 1.")
            .OverridePropertyName("page");

        RuleFor(request => request.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("pageSize must be greater than or equal to 1.")
            .OverridePropertyName("pageSize")
            .LessThanOrEqualTo(500)
            .WithMessage("pageSize cannot exceed 500.")
            .OverridePropertyName("pageSize");
    }
}
