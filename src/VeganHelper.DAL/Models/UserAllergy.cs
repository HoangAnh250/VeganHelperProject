namespace VeganHelper.DAL.Models;

public sealed class UserAllergy
{
    public long UserId { get; set; }
    public long IngredientId { get; set; }
    public DateTime CreatedAt { get; set; }
}
