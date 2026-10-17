using VeganHelper.BLL.DTOs.MealPlanning;

namespace VeganHelper.BLL.Contracts;

public interface IPayOSGateway
{
    bool IsConfigured { get; }

    Task<PayOSCreatedPaymentLink> CreatePaymentLinkAsync(
        PayOSCreatePaymentLink request,
        CancellationToken cancellationToken);

    Task<VerifiedPayOSPayment> VerifyWebhookAsync(
        string payload,
        CancellationToken cancellationToken);
}
