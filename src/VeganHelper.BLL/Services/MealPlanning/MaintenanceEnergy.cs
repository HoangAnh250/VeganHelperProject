using VeganHelper.DAL.Entities;

namespace VeganHelper.BLL.Services.MealPlanning;

public static class MaintenanceEnergy
{
    public static string? ValidationIssue(UserProfile? profile, DateOnly today, int minimumAge = 18)
    {
        if (profile is null) return "Complete your health profile before generating a weekly menu.";
        if (profile.BirthDate is null) return "Add your date of birth to your health profile.";
        var birth = profile.BirthDate!.Value;
        var age = today.Year - birth.Year;
        if (birth > today.AddYears(-age)) age--;
        if (age < minimumAge || age > 120) return $"Weekly menus require an age between {minimumAge} and 120 years.";
        if (profile.HeightCm is not >= 100m or > 250m) return "Enter a height between 100 and 250 cm in your health profile.";
        if (profile.WeightKg is not >= 30m or > 200m) return "Enter a weight between 30 and 200 kg in your health profile.";
        if (profile.BiologicalSex is not ("male" or "female")) return "This calorie estimate requires male or female biological sex in your health profile.";
        if (profile.ActivityLevel is not ("sedentary" or "light" or "moderate" or "active" or "very_active"))
            return "Choose an activity level in your health profile.";
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
