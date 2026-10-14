namespace VeganHelper.BLL.DTOs.Posts;

public class PostIngredientDto
{
    public long IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}
