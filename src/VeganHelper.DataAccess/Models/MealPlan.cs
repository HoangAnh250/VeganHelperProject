namespace VeganHelper.DataAccess.Models;

public sealed class MealPlan
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public decimal BmiValue { get; set; }
    public string DietType { get; set; } = string.Empty;
    public string AllergiesSnapshot { get; set; } = string.Empty;
    public string AvailableIngredientsSnapshot { get; set; } = string.Empty;
    public string GenerationSource { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? SavedAt { get; set; }
}
