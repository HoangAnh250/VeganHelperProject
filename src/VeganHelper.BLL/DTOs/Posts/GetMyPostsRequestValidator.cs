using FluentValidation;

namespace VeganHelper.BLL.DTOs.Posts;

public class GetMyPostsRequestValidator : AbstractValidator<GetMyPostsRequest>
{
    public GetMyPostsRequestValidator()
    {
        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("PageIndex must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x.Status)
            .Must(x => string.IsNullOrEmpty(x) || x == "published" || x == "pending_review" || x == "draft" || x == "rejected")
            .WithMessage("Status must be published, pending_review, draft, or rejected.");
    }
}
