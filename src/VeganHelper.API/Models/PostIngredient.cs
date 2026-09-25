namespace VeganHelper.API.Models;

public sealed class PostIngredient
{
    public long PostId { get; set; }
    public long IngredientId { get; set; }
    public decimal? Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}
