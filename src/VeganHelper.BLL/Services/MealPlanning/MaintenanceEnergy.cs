using VeganHelper.DAL.Entities;

namespace VeganHelper.BLL.Services.MealPlanning;

public static class MaintenanceEnergy
{
    public static decimal? Estimate(UserProfile profile, DateOnly today, int minimumAge = 18)
    {
        if (profile.HeightCm is not >= 100m or > 250m || profile.WeightKg is not >= 30m or > 200m
            || profile.BirthDate is null || profile.BiologicalSex is not ("male" or "female")) return null;
        var birth = profile.BirthDate.Value;
        var age = today.Year - birth.Year;
        if (birth > today.AddYears(-age)) age--;
        if (age < minimumAge || age > 120) return null;
        var factor = profile.ActivityLevel switch
        {
            "sedentary" => 1.2m, "light" => 1.375m, "moderate" => 1.55m,
            "active" => 1.725m, "very_active" => 1.9m, _ => 0m
        };
        if (factor == 0) return null;
        var resting = 10m*profile.WeightKg.Value + 6.25m*profile.HeightCm.Value - 5m*age
            + (profile.BiologicalSex == "male" ? 5m : -161m);
        return Math.Round(resting*factor, 2);
    }
}
