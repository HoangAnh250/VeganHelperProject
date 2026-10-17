namespace VeganHelper.DAL.Entities;

public sealed class UserMealPlanAccess
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public int MealPlanPackageId { get; set; }
    public long PaymentOrderId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
