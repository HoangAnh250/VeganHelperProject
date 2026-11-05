using VeganHelper.DAL.Entities;

namespace VeganHelper.BLL.Services.MealPlanning;

public static class MaintenanceEnergy
{
    public static string? ValidationIssue(UserProfile? profile, DateOnly today, int minimumAge = 18)
    {
        if (profile is null) return "Add your health profile details so we can estimate the energy you need for your weekly menu.";
        if (profile.BirthDate is null) return "Add your date of birth in Health & Wellness so we can use your age in the calorie estimate.";
        var birth = profile.BirthDate!.Value;
        var age = today.Year - birth.Year;
        if (birth > today.AddYears(-age)) age--;
        if (age < minimumAge || age > 120) return $"Weekly menus require an age between {minimumAge} and 120 years.";
        if (profile.HeightCm is not >= 100m or > 250m) return "Check the height in your health profile. The planner can estimate calories for heights between 100 and 250 cm.";
        if (profile.WeightKg is not >= 30m or > 200m) return "Check the weight in your health profile. If it is outside the range of 30 to 200 kg, a health professional can offer more individual guidance.";
        if (profile.BiologicalSex is not ("male" or "female")) return "Review the biological sex field in your health profile. The calorie formula uses one of the two supported options.";
        if (profile.ActivityLevel is not ("sedentary" or "light" or "moderate" or "active" or "very_active"))
            return "Choose the activity level that best reflects your usual week so we can estimate your energy needs.";
        return null;
    }

    public static decimal? Estimate(UserProfile profile, DateOnly today, int minimumAge = 18)
    {
        if (ValidationIssue(profile, today, minimumAge) is not null) return null;
        var birth = profile.BirthDate!.Value;
        var age = today.Year - birth.Year;
        if (birth > today.AddYears(-age)) age--;
        if (age < minimumAge || age > 120) return null;
        var factor = profile.ActivityLevel switch
        {
            "sedentary" => 1.2m, "light" => 1.375m, "moderate" => 1.55m,
            "active" => 1.725m, "very_active" => 1.9m, _ => 0m
        };
        if (factor == 0) return null;
        var resting = 10m*profile.WeightKg!.Value + 6.25m*profile.HeightCm!.Value - 5m*age
            + (profile.BiologicalSex == "male" ? 5m : -161m);
        return Math.Round(resting*factor, 2);
    }
}
