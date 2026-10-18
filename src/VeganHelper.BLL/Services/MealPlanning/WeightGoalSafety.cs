using VeganHelper.DAL.Entities;

namespace VeganHelper.BLL.Services.MealPlanning;

public sealed record GoalSafetyResult(bool Allowed, decimal Tdee, decimal TargetCalories,
    decimal DailyAdjustment, decimal TargetBmi, string PolicyVersion, List<string> Reasons);

/// <summary>Conservative product screening, not a clinical assessment or a guaranteed outcome.</summary>
public static class WeightGoalSafety
{
    public const string PolicyVersion="goal-safety-v1";
    public static GoalSafetyResult Evaluate(UserProfile? profile,DateOnly asOf,decimal targetWeight,
        DateOnly targetDate,bool eligibilityConfirmed)
    {
        var reasons=new List<string>();
        var tdee=profile is null ? null : MaintenanceEnergy.Estimate(profile,asOf,20);
        if(tdee is null) reasons.Add("Complete a supported adult health profile before setting a weight goal.");
        if(targetWeight is <30 or >200) reasons.Add("Target weight must be between 30 and 200 kg.");
        if(tdee is null || targetWeight is <30 or >200)
            return new(false,tdee ?? 0,0,0,0,PolicyVersion,reasons);
        if(profile?.BirthDate is DateOnly birth && birth<=asOf.AddYears(-66))
            reasons.Add("Weight goal estimates are available for adults aged 20–65. Ask a qualified professional for individual advice.");
        if(!eligibilityConfirmed) reasons.Add("Confirm that you are not pregnant or breastfeeding and do not need clinician-managed nutrition.");
        var days=targetDate.DayNumber-asOf.DayNumber;
        if(days<7 || days>730) reasons.Add("Choose a target date between 7 and 730 days from the calculation date.");
        var height=profile?.HeightCm/100m;
        var bmi=height>0 ? targetWeight/height.Value/height.Value : 0;
        if(targetWeight is <30 or >200 || bmi is <18.5m or >=25m)
            reasons.Add("The requested target weight must have an estimated BMI from 18.5 to below 25.");
        var currentBmi=height>0 && profile?.WeightKg is decimal weight ? weight/height.Value/height.Value : 0;
        if(currentBmi is <18.5m or >=40m)
            reasons.Add("Your current measurements require individual professional advice before automated weight goal planning.");
        var delta=targetWeight-(profile?.WeightKg ?? targetWeight);
        var adjustment=days>0 ? Math.Round(delta*7700m/days,2) : 0;
        if(days>0 && Math.Abs(delta)*7m/days>.5m)
            reasons.Add("The requested weight change exceeds this application's limit of 0.5 kg per week. Extend your timeline.");
        if(tdee is decimal energy && (Math.Abs(adjustment)>500m || Math.Abs(adjustment)>energy*.2m))
            reasons.Add("The required calorie deficit or surplus exceeds 500 kcal/day or 20% of your estimated TDEE. Extend your timeline.");
        var target=(tdee ?? 0)+adjustment;
        var minimum=profile?.BiologicalSex=="male" ? 1500m : 1200m;
        if(tdee is not null && target<minimum)
            reasons.Add($"The estimated daily intake falls below this application's {minimum:0} kcal minimum. Change the goal or timeline.");
        return new(reasons.Count==0,tdee ?? 0,Math.Round(target,2),adjustment,Math.Round(bmi,2),PolicyVersion,reasons);
    }

    public static void ValidateGeneratedDays(IEnumerable<VeganHelper.BLL.DTOs.MealPlanning.MealDayDto> days,
        GoalSafetyResult safety,string sex)
    {
        var minimum=sex=="male"?1500m:1200m;
        if(days.Any(d=>d.TotalCalories<minimum || Math.Abs(d.TotalCalories-safety.Tdee)>500m
            || Math.Abs(d.TotalCalories-safety.Tdee)>safety.Tdee*.2m))
            throw new VeganHelper.BLL.Exceptions.ConflictException("No menu fits the calorie safety limits. Try a longer timeline or a broader suitable meal catalog.");
    }
}
