namespace VeganHelper.DAL.Entities;

public sealed class MealPlanPaymentOrder
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public int MealPlanPackageId { get; set; }
    public long OrderCode { get; set; }
    public string PackageCodeSnapshot { get; set; } = string.Empty;
    public string PackageNameSnapshot { get; set; } = string.Empty;
    public int DurationMonthsSnapshot { get; set; }
    public int AmountVnd { get; set; }
    public string Status { get; set; } = "pending";
    public string? PaymentLinkId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? ProviderReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}
