using FluentValidation;

namespace VeganHelper.BLL.DTOs.MealPlanning;

public sealed class MealCalorieSplitDto
{
    public int BreakfastPercent { get; set; }
    public int LunchPercent { get; set; }
    public int DinnerPercent { get; set; }

    public bool IsValid() =>
        BreakfastPercent is > 0 and < 100 &&
        LunchPercent is > 0 and < 100 &&
        DinnerPercent is > 0 and < 100 &&
        BreakfastPercent + LunchPercent + DinnerPercent == 100;

    // Used only for requests from older clients and snapshots created before user-defined splits.
    public static MealCalorieSplitDto LegacyDefault() => new()
    {
        BreakfastPercent = 30,
        LunchPercent = 35,
        DinnerPercent = 35
    };
}

public sealed class GenerateMealPlanRequest
{
    public DateOnly StartDate { get; set; }
    public string Mode { get; set; }="maintenance";
    public decimal? TargetWeightKg { get; set; }
    public DateOnly? TargetDate { get; set; }
    public bool EligibilityConfirmed { get; set; }
    public string TimeZoneId { get; set; }="UTC";
    public bool RemindersEnabled { get; set; }
    public TimeOnly BreakfastTime { get; set; }=new(7,30);
    public TimeOnly LunchTime { get; set; }=new(12,0);
    public TimeOnly DinnerTime { get; set; }=new(18,30);
    public MealCalorieSplitDto? MealCalorieSplit { get; set; }
}

public sealed class GenerateMealPlanRequestValidator : AbstractValidator<GenerateMealPlanRequest>
{
    public GenerateMealPlanRequestValidator()
    {
        RuleFor(x=>x.StartDate).NotEmpty().Must(d=>d >= new DateOnly(2000,1,1) && d <= new DateOnly(2100,12,25))
            .WithMessage("Start date must be between 2000-01-01 and 2100-12-25.");
        RuleFor(x=>x.MealCalorieSplit)
            .Must(split => split is null || split.IsValid())
            .WithMessage("Breakfast, lunch and dinner percentages must each be between 1 and 98 and total exactly 100.");
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
    public MealRoutineDto? Routine { get; set; }
}

public sealed class MealRoutineDto
{
    public string Mode { get; set; }="maintenance";
    public decimal? TargetWeightKg { get; set; }
    public DateOnly? TargetDate { get; set; }
    public long? PreviousPlanId { get; set; }
    public string TimeZoneId { get; set; }="UTC";
    public bool RemindersEnabled { get; set; }
    public TimeOnly BreakfastTime { get; set; }
    public TimeOnly LunchTime { get; set; }
    public TimeOnly DinnerTime { get; set; }
    public string State { get; set; }="active";
    public int Version { get; set; }
    public List<MealEntryDto> Entries { get; set; }=[];
}

public sealed class MealEntryDto
{
    public DateOnly Date { get; set; }
    public string MealTime { get; set; }="";
    public string Status { get; set; }="unlogged";
    public string? Note { get; set; }
    public DateTime DueAt { get; set; }
}
public sealed record LogMealRequest(DateOnly Date,string MealTime,string Status,string? Note,int ExpectedVersion);
public sealed record MealReminderSettingsRequest(bool Enabled,string TimeZoneId,TimeOnly BreakfastTime,
    TimeOnly LunchTime,TimeOnly DinnerTime,int ExpectedVersion);
public sealed record RevisionRequest(int ExpectedVersion,string ProfileFingerprint,bool EligibilityConfirmed,
    decimal? TargetWeightKg=null,DateOnly? TargetDate=null,bool NextWeek=false);
public sealed record RevisionPreview(bool Allowed,List<string> Reasons,int Version,string ProfileFingerprint,
    decimal PreviousCalories,decimal TargetCalories,decimal Tdee,decimal DailyAdjustment,DateOnly EffectiveFrom,
    bool SignificantChange,bool NextWeek,string PolicyVersion);

public sealed class WeeklyMenuDto
{
    public string AlgorithmVersion { get; set; } = "rules-v1";
    public string Goal { get; set; } = "maintenance";
    public string DietType { get; set; } = "";
    public decimal Bmi { get; set; }
    public decimal TargetDailyCalories { get; set; }
    public MealCalorieSplitDto? MealCalorieSplit { get; set; }
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
    public string? ImageUrl { get; set; }
    public string MealTime { get; set; } = "";
    public decimal PortionMultiplier { get; set; }
    public decimal Calories { get; set; }
    public List<MealIngredientDto> Ingredients { get; set; } = [];
    public List<string> CookingSteps { get; set; } = [];
}

public sealed class MealIngredientDto
{
    public long IngredientId { get; set; }
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
}
