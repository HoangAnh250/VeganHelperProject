namespace VeganHelper.DAL.Models;

public sealed class PostIngredient
{
    public long PostId { get; set; }
    public long IngredientId { get; set; }
    public decimal? Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;

    public Ingredient? Ingredient { get; set; }
}
