using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public enum PaymentWebhookApplyResult
{
    Applied,
    AlreadyApplied,
    UnknownOrder,
    AmountMismatch,
    PaymentLinkMismatch
}

public interface IMealPlanPaymentRepository
{
    Task<IReadOnlyList<MealPlanPackage>> GetActivePackagesAsync(CancellationToken ct);
    Task<MealPlanPackage?> GetActivePackageAsync(string code, CancellationToken ct);
    Task<MealPlanPaymentOrder> CreatePendingOrderAsync(long userId, MealPlanPackage package, DateTime now, CancellationToken ct);
    Task SaveCheckoutLinkAsync(long orderId, string paymentLinkId, string checkoutUrl, DateTime now, CancellationToken ct);
    Task MarkOrderFailedAsync(long orderId, DateTime now, CancellationToken ct);
    Task<MealPlanPaymentOrder?> GetOrderForUserAsync(long orderId, long userId, CancellationToken ct);
    Task<UserMealPlanAccess?> GetLatestAccessAsync(long userId, CancellationToken ct);
    Task<bool> HasActiveAccessAsync(long userId, DateTime now, CancellationToken ct);
    Task<PaymentWebhookApplyResult> ApplyWebhookAsync(
        long orderCode,
        int amountVnd,
        bool successful,
        string currency,
        string? paymentLinkId,
        string? providerReference,
        DateTime receivedAt,
        CancellationToken ct);
}
