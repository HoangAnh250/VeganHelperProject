using FluentValidation;

namespace VeganHelper.BLL.DTOs.Comments;

public sealed class CreateCommentRequestValidator : AbstractValidator<CreateCommentRequest>
{
    public CreateCommentRequestValidator()
    {
        RuleFor(request => request.Content)
            .Must(content => content is not null && content.Trim().Length is >= 1 and <= 500)
            .WithMessage("Comment content must contain 1 to 500 characters.");

        RuleFor(request => request.ParentCommentId)
            .GreaterThan(0)
            .When(request => request.ParentCommentId.HasValue)
            .WithMessage("Parent comment ID must be greater than zero.");
    }
}
