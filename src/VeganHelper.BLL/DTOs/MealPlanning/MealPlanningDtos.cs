using FluentValidation;

namespace VeganHelper.BLL.DTOs.MealPlanning;

public sealed class GenerateMealPlanRequest
{
    public DateOnly StartDate { get; set; }
}

public sealed class GenerateMealPlanRequestValidator : AbstractValidator<GenerateMealPlanRequest>
{
    public GenerateMealPlanRequestValidator()
    {
        RuleFor(x=>x.StartDate).NotEmpty().Must(d=>d >= new DateOnly(2000,1,1) && d <= new DateOnly(2100,12,25))
            .WithMessage("Start date must be between 2000-01-01 and 2100-12-25.");
    }
}

public sealed class MealPlanDto
{
    public long Id { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = "";
    public string DietType { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public WeeklyMenuDto? Menu { get; set; }
}

public sealed class WeeklyMenuDto
{
    public string AlgorithmVersion { get; set; } = "rules-v1";
    public string Goal { get; set; } = "maintenance";
    public string DietType { get; set; } = "";
    public decimal Bmi { get; set; }
    public decimal TargetDailyCalories { get; set; }
    public EnergyProfileDto EnergyProfile { get; set; } = new();
    public List<string> Allergies { get; set; } = [];
    public List<string> Notes { get; set; } = [];
    public List<MealDayDto> Days { get; set; } = [];
}

public sealed class EnergyProfileDto
{
    public DateOnly EstimatedOn { get; set; }
    public DateOnly BirthDate { get; set; }
    public decimal HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public string BiologicalSex { get; set; } = "";
    public string ActivityLevel { get; set; } = "";
}

public sealed class MealDayDto
{
    public byte DayIndex { get; set; }
    // ISO weekday: Monday=1, Sunday=7. DayIndex is relative to the plan's start.
    public byte DayOfWeek { get; set; }
    public DateOnly Date { get; set; }
    public decimal TotalCalories { get; set; }
    public List<ScheduledMealDto> Meals { get; set; } = [];
}

public sealed class ScheduledMealDto
{
    public long MealId { get; set; }
    public string Name { get; set; } = "";
    public string MealTime { get; set; } = "";
    public decimal PortionMultiplier { get; set; }
    public decimal Calories { get; set; }
    public List<MealIngredientDto> Ingredients { get; set; } = [];
}

public sealed class MealIngredientDto
{
    public long IngredientId { get; set; }
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
}
