namespace VeganHelper.DAL.Entities;

public sealed class MealIngredient
{
    public long MealId { get; set; }
    public long IngredientId { get; set; }
    public decimal? CalculatedQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}
