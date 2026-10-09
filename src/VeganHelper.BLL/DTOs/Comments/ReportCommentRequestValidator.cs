using FluentValidation;

namespace VeganHelper.BLL.DTOs.Comments;

public sealed class ReportCommentRequestValidator : AbstractValidator<ReportCommentRequest>
{
    public ReportCommentRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Must(reason => reason is not null && reason.Trim().Length is >= 1 and <= 500)
            .WithMessage("Report reason must contain 1 to 500 characters.");
    }
}
