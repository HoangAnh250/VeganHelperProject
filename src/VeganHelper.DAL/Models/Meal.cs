namespace VeganHelper.DAL.Models;

public sealed class Meal
{
    public long Id { get; set; }
    public long? OwnerUserId { get; set; }
    public string ComboName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Servings { get; set; }
    public decimal? TotalCalories { get; set; }
    public string DietType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
