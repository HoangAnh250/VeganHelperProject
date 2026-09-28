namespace VeganHelper.BLL.DTOs.Posts;

using FluentValidation;

public class CreatePostRequestValidator : AbstractValidator<CreatePostRequest>
{
    public CreatePostRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(255).WithMessage("Title cannot exceed 255 characters.");

        RuleFor(x => x.PostType)
            .NotEmpty().WithMessage("PostType is required.")
            .Must(x => x == "article" || x == "video" || x == "recipe" || x == "community")
            .WithMessage("Invalid PostType.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("CategoryId is required and must be greater than 0.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.");

        RuleFor(x => x.DifficultyLevel)
            .Must(x => string.IsNullOrEmpty(x) || x == "easy" || x == "medium" || x == "hard")
            .WithMessage("DifficultyLevel must be easy, medium, or hard.");

        RuleFor(x => x.DietType)
            .Must(x => string.IsNullOrEmpty(x) || x == "vegan" || x == "lacto_ovo_vegetarian")
            .WithMessage("DietType must be vegan or lacto_ovo_vegetarian.");

        RuleFor(x => x.PrepTimeMins)
            .GreaterThanOrEqualTo(0).When(x => x.PrepTimeMins.HasValue);

        RuleFor(x => x.CookingTimeMins)
            .GreaterThanOrEqualTo(0).When(x => x.CookingTimeMins.HasValue);

        RuleFor(x => x.MediaFiles)
            .NotNull().WithMessage("At least 1 media file is required for thumbnail.")
            .Must(x => x != null && x.Count > 0).WithMessage("At least 1 media file is required.");
    }
}
