using FluentValidation;

namespace VeganHelper.BLL.DTOs.Posts;

public class UpdatePostRequestValidator : AbstractValidator<UpdatePostRequest>
{
    public UpdatePostRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(255).WithMessage("Title cannot exceed 255 characters.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("CategoryId must be greater than 0.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.");

        RuleFor(x => x.DifficultyLevel)
            .Must(x => string.IsNullOrEmpty(x) || x == "easy" || x == "medium" || x == "hard")
            .WithMessage("DifficultyLevel must be easy, medium, or hard.");

        RuleFor(x => x.DietType)
            .Must(x => string.IsNullOrEmpty(x) || x == "vegan" || x == "lacto_ovo_vegetarian")
            .WithMessage("DietType must be vegan or lacto_ovo_vegetarian.");
            
        RuleFor(x => x.PrepTimeMins)
            .GreaterThan(0).When(x => x.PrepTimeMins.HasValue).WithMessage("PrepTimeMins must be greater than 0.");

        RuleFor(x => x.CookingTimeMins)
            .GreaterThan(0).When(x => x.CookingTimeMins.HasValue).WithMessage("CookingTimeMins must be greater than 0.");
    }
}
