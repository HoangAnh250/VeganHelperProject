using FluentValidation;

namespace VeganHelper.BLL.DTOs.HealthProfile;

public sealed class UpdateHealthProfileRequestValidator : AbstractValidator<UpdateHealthProfileRequest>
{
    public UpdateHealthProfileRequestValidator()
    {
        RuleFor(x => x.HeightCm).InclusiveBetween(100m, 250m);
        RuleFor(x => x.WeightKg).InclusiveBetween(30m, 200m);
        RuleFor(x => x.BiologicalSex).Must(v => v is "male" or "female" or "other");
        RuleFor(x => x.DietType).Must(v => v is "vegan" or "lacto_ovo_vegetarian");
        RuleFor(x => x.ActivityLevel).Must(v => v is "sedentary" or "light" or "moderate" or "active" or "very_active");
        RuleFor(x => x.BirthDate).Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow) && date >= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-120))
            .WithMessage("BirthDate must be a valid date within the past 120 years.");
    }
}

public sealed class DeclareAllergiesRequestValidator : AbstractValidator<DeclareAllergiesRequest>
{
    public DeclareAllergiesRequestValidator()
    {
        RuleFor(x => x.AllergyIngredientIds).NotNull().Must(ids => ids is null || ids.Count <= 100);
        RuleForEach(x => x.AllergyIngredientIds).GreaterThan(0);
        RuleFor(x => x.CustomAllergies).NotNull().Must(names => names is null || names.Count <= 50);
        RuleForEach(x => x.CustomAllergies).NotEmpty().Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 100)
            .WithMessage("Each custom allergy must contain 1–100 characters.");
    }
}
