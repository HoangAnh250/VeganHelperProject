using VeganHelper.DAL.Entities;

namespace VeganHelper.BLL.Services.MealPlanning;

public sealed record GoalSafetyResult(bool Allowed, decimal Tdee, decimal TargetCalories,
    decimal DailyAdjustment, decimal TargetBmi, string PolicyVersion, List<string> Reasons);

/// <summary>Conservative product screening, not a clinical assessment or a guaranteed outcome.</summary>
public static class WeightGoalSafety
{
    public const string PolicyVersion="goal-safety-v2";
    public static GoalSafetyResult Evaluate(UserProfile? profile,DateOnly asOf,decimal targetWeight,
        DateOnly targetDate,bool eligibilityConfirmed)
    {
        var reasons=new List<string>();
        if(profile?.HeightCm is >=100m and <=250m && profile.WeightKg is decimal measuredWeight)
        {
            var heightMetres=profile.HeightCm.Value/100m;
            if(measuredWeight/(heightMetres*heightMetres)>40m)
                return new(false,0,0,0,0,PolicyVersion,
                    ["A doctor or registered dietitian can help you find an approach that supports your health and fits your needs. Weekly Menu cannot assess those needs closely enough to create a weight loss plan for you."]);
        }
        var profileIssue=MaintenanceEnergy.ValidationIssue(profile,asOf);
        var tdee=profileIssue is null ? MaintenanceEnergy.Estimate(profile!,asOf) : null;
        if(profileIssue is not null) reasons.Add(profileIssue);
        if(targetWeight is <30 or >200) reasons.Add("For automatic planning, choose a target weight between 30 and 200 kg. If your goal is outside this range, consider individual guidance from a health professional.");
        if(tdee is null || targetWeight is <30 or >200)
            return new(false,tdee ?? 0,0,0,0,PolicyVersion,reasons);
        if(profile?.BirthDate is DateOnly birth && birth<=asOf.AddYears(-66))
            reasons.Add("Weight goal estimates are available for adults aged 18–65. Ask a qualified professional for individual advice.");
        if(!eligibilityConfirmed) reasons.Add("If you are pregnant, breastfeeding or need clinician-managed nutrition, ask your care team about a plan tailored to you. Otherwise, confirm that this guidance applies to you.");
        var days=targetDate.DayNumber-asOf.DayNumber;
        if(days<7 || days>730) reasons.Add("Give yourself at least a week for a gradual change. For a goal more than 730 days away, consider setting a smaller first milestone and choosing a target date for it.");
        var height=profile?.HeightCm/100m;
        var bmi=height>0 ? targetWeight/height.Value/height.Value : 0;
        var losingWeight=targetWeight<profile!.WeightKg;
        if(bmi<18.5m)
            reasons.Add("This target weight would put your estimated BMI below 18.5. Choose a higher target weight to avoid aiming for an underweight range.");
        else if(bmi>=25m && !losingWeight)
            reasons.Add("At this estimated BMI, the planner supports gradual weight loss. Choose a target weight below your current weight, or use a balanced menu to maintain it.");
        var currentBmi=height>0 && profile?.WeightKg is decimal weight ? weight/height.Value/height.Value : 0;
        if(currentBmi<18.5m)
            reasons.Add("Given your current measurements, a doctor or registered dietitian can help you choose a goal that supports your health.");
        var delta=targetWeight-(profile?.WeightKg ?? targetWeight);
        var adjustment=days>0 ? Math.Round(delta*7700m/days,2) : 0;
        var paceTooFast=days>0 && Math.Abs(delta)*7m/days>.5m;
        var adjustmentTooLarge=tdee is decimal energy && (Math.Abs(adjustment)>500m || Math.Abs(adjustment)>energy*.2m);
        if(paceTooFast || adjustmentTooLarge)
            reasons.Add(losingWeight
                ? "Losing this much by your target date would require cutting calories too sharply, which could leave you tired or low on energy. Choose a later target date to give yourself more time."
                : "Gaining this much by your target date would require increasing your calorie intake too quickly. Choose a later target date for a more gradual change.");
        var target=(tdee ?? 0)+adjustment;
        var minimum=profile?.BiologicalSex=="male" ? 1500m : 1200m;
        if(tdee is not null && target<minimum && !paceTooFast && !adjustmentTooLarge)
            reasons.Add("This goal would leave too little energy in your daily meals. Try a later target date or a smaller change. If that still does not work, ask a dietitian for individual advice.");
        return new(reasons.Count==0,tdee ?? 0,Math.Round(target,2),adjustment,Math.Round(bmi,2),PolicyVersion,reasons);
    }

    public static void ValidateGeneratedDays(IEnumerable<VeganHelper.BLL.DTOs.MealPlanning.MealDayDto> days,
        GoalSafetyResult safety,string sex)
    {
        var minimum=sex=="male"?1500m:1200m;
        if(days.Any(d=>d.TotalCalories<minimum || Math.Abs(d.TotalCalories-safety.Tdee)>500m
            || Math.Abs(d.TotalCalories-safety.Tdee)>safety.Tdee*.2m))
            throw new VeganHelper.BLL.Exceptions.ConflictException("We could not put together a full week within the calorie limits for this goal. Try a later target date, a smaller change, or a different split across meals. Keep any allergies you have recorded.");
    }
}
