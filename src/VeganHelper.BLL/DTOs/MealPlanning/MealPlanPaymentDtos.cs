namespace VeganHelper.BLL.DTOs.MealPlanning;

public sealed record MealPlanPackageDto(
    string Code,
    string Name,
    int DurationMonths,
    int AmountVnd);

public sealed class CreateMealPlanCheckoutRequest
{
    public string PackageCode { get; set; } = string.Empty;
}

public sealed record MealPlanCheckoutDto(
    long OrderId,
    long OrderCode,
    string CheckoutUrl,
    int AmountVnd,
    string Status,
    DateTime CreatedAt);

public sealed record MealPlanPaymentOrderDto(
    long OrderId,
    string PackageCode,
    string PackageName,
    int DurationMonths,
    int AmountVnd,
    string Status,
    DateTime CreatedAt,
    DateTime? PaidAt);

public sealed record MealPlanAccessDto(
    string Status,
    bool CanGenerate,
    bool CanViewExistingPlans,
    DateTime? ExpiresAt,
    DateTime? GraceEndsAt,
    int TrialGenerationsRemaining,
    bool CanViewTrialPlans);

public sealed record PayOSWebhookAcknowledgement(string Code, string Desc);

public sealed record PayOSCreatePaymentLink(
    long OrderCode,
    int AmountVnd,
    string Description,
    string ReturnUrl,
    string CancelUrl);

public sealed record PayOSCreatedPaymentLink(
    string PaymentLinkId,
    string CheckoutUrl);

public sealed record VerifiedPayOSPayment(
    long OrderCode,
    int AmountVnd,
    bool IsSuccessful,
    string Currency,
    string? PaymentLinkId,
    string? ProviderReference);
