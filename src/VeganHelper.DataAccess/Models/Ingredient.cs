namespace VeganHelper.DataAccess.Models;

public sealed class Ingredient
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DefaultUnit { get; set; } = string.Empty;
    public decimal? CaloriesPer100g { get; set; }
}
