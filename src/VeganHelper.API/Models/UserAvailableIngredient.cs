namespace VeganHelper.API.Models;

public sealed class UserAvailableIngredient
{
    public long UserId { get; set; }
    public long IngredientId { get; set; }
    public decimal? Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
