using System.Globalization;

namespace VeganHelper.BLL.Services.MealPlanning;

public static class MealPlanNaming
{
    public const int MaximumLength = 100;

    public static string Default(DateOnly startDate) =>
        $"Week of {startDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";

    public static string NormalizeOrDefault(string? name, DateOnly startDate)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrEmpty(normalized)) return Default(startDate);
        if (normalized.Length > MaximumLength)
            throw new ArgumentException($"Menu name must be {MaximumLength} characters or fewer.");
        return normalized;
    }

    public static string NormalizeRequired(string? name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrEmpty(normalized))
            throw new ArgumentException("Menu name is required.");
        if (normalized.Length > MaximumLength)
            throw new ArgumentException($"Menu name must be {MaximumLength} characters or fewer.");
        return normalized;
    }
}
