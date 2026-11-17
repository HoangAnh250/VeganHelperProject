namespace VeganHelper.DAL.Entities;

public static class BodyMeasurements
{
    public const decimal MinHeight = 100;
    public const decimal MaxHeight = 250;
    public const decimal MinWeight = 30;
    public const decimal MaxWeight = 200;

    public static decimal CalculateBmi(decimal kg, decimal cm) => Math.Round(kg / ((cm / 100m) * (cm / 100m)), 2);

    public static BmiHistory? Apply(UserProfile profile, decimal? height, decimal? weight)
    {
        if (height is null && weight is null) return null;
        var cm = height.HasValue ? Math.Round(height.Value, 2) : profile.HeightCm;
        var kg = weight.HasValue ? Math.Round(weight.Value, 2) : profile.WeightKg;
        var changed = profile.HeightCm != cm || profile.WeightKg != kg;
        profile.HeightCm = cm;
        profile.WeightKg = kg;
        profile.CurrentBmi = cm is >= MinHeight and <= MaxHeight && kg is >= MinWeight and <= MaxWeight
            ? CalculateBmi(kg.Value, cm.Value) : null;

        return changed && profile.CurrentBmi is decimal bmi
            ? new BmiHistory { UserId = profile.UserId, HeightCm = cm!.Value, WeightKg = kg!.Value, BmiValue = bmi, RecordedAt = DateTime.UtcNow }
            : null;
    }
}
