using FluentValidation;

namespace VeganHelper.BLL.DTOs.Comments;

public sealed class UpdateCommentRequestValidator : AbstractValidator<UpdateCommentRequest>
{
    public UpdateCommentRequestValidator()
    {
        RuleFor(request => request.Content)
            .Must(content => content is not null && content.Trim().Length is >= 1 and <= 500)
            .WithMessage("Comment content must contain 1 to 500 characters.");
    }
}
