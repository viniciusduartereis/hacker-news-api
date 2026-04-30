using FluentValidation;
using HackerNewsApi.Features.Stories.Contracts;

namespace HackerNewsApi.Features.Stories.Validators;

public sealed class GetBestStoriesRequestValidator : AbstractValidator<GetBestStoriesRequest>
{
    public GetBestStoriesRequestValidator()
    {
        RuleFor(request => request.Count)
            .GreaterThan(0)
            .WithMessage("count must be a positive integer.")
            .OverridePropertyName("count")
            .LessThanOrEqualTo(500)
            .WithMessage("count cannot exceed 500 (maximum returned by the HN best-stories endpoint).")
            .OverridePropertyName("count");
    }
}
