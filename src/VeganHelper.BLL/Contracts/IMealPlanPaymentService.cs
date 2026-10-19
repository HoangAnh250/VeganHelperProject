using VeganHelper.BLL.DTOs.MealPlanning;

namespace VeganHelper.BLL.Contracts;

public interface IMealPlanPaymentService
{
    Task<IReadOnlyList<MealPlanPackageDto>> GetPackagesAsync(CancellationToken ct);
    Task<MealPlanCheckoutDto> CreateCheckoutAsync(long userId, string packageCode, CancellationToken ct);
    Task<MealPlanPaymentOrderDto> GetOrderAsync(long userId, long orderId, CancellationToken ct);
    Task<MealPlanAccessDto> GetAccessAsync(long userId, CancellationToken ct);
    Task<PayOSWebhookAcknowledgement> HandleWebhookAsync(string payload, CancellationToken ct);
}
