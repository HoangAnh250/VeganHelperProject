using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class MealPlanPaymentService(
    IMealPlanPaymentRepository repository,
    IMealPlanningRepository mealPlanningRepository,
    IPayOSGateway payOS,
    IOptions<PayOSOptions> payOSOptions,
    TimeProvider clock,
    ILogger<MealPlanPaymentService> logger) : IMealPlanPaymentService
{
    public async Task<IReadOnlyList<MealPlanPackageDto>> GetPackagesAsync(CancellationToken ct) =>
        (await repository.GetActivePackagesAsync(ct))
        .Select(package => new MealPlanPackageDto(package.Code, package.Name, package.DurationMonths, package.AmountVnd))
        .ToList();

    public async Task<MealPlanCheckoutDto> CreateCheckoutAsync(long userId, string packageCode, CancellationToken ct)
    {
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
        if (string.IsNullOrWhiteSpace(packageCode) || packageCode.Length > 40)
            throw new ArgumentException("A valid meal-plan package code is required.");
        if (!payOS.IsConfigured)
            throw new PaymentProviderException("The payment service is not configured yet.");

        var settings = payOSOptions.Value;
        var returnUrl = ValidateRedirectUrl(settings.ReturnUrl, "return");
        var cancelUrl = ValidateRedirectUrl(settings.CancelUrl, "cancel");
        var package = await repository.GetActivePackageAsync(packageCode.Trim(), ct)
            ?? throw new NotFoundException("Meal-plan package was not found.");

        var now = clock.GetUtcNow().UtcDateTime;
        var order = await repository.CreatePendingOrderAsync(userId, package, now, ct);

        PayOSCreatedPaymentLink checkout;
        try
        {
            checkout = await payOS.CreatePaymentLinkAsync(new PayOSCreatePaymentLink(
                order.OrderCode,
                order.AmountVnd,
                "VHPLAN",
                returnUrl,
                cancelUrl), ct);
            await repository.SaveCheckoutLinkAsync(order.Id, checkout.PaymentLinkId, checkout.CheckoutUrl, clock.GetUtcNow().UtcDateTime, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await repository.MarkOrderFailedAsync(order.Id, clock.GetUtcNow().UtcDateTime, ct);
            logger.LogWarning("PayOS checkout creation failed for order {OrderId} ({ExceptionType}).", order.Id, exception.GetType().Name);
            throw new PaymentProviderException("Could not create a PayOS checkout link. Please try again later.");
        }

        return new MealPlanCheckoutDto(order.Id, order.OrderCode, checkout.CheckoutUrl, order.AmountVnd, order.Status, order.CreatedAt);
    }

    public async Task<MealPlanPaymentOrderDto> GetOrderAsync(long userId, long orderId, CancellationToken ct)
    {
        var order = await repository.GetOrderForUserAsync(orderId, userId, ct)
            ?? throw new NotFoundException("Payment order was not found.");
        return new MealPlanPaymentOrderDto(
            order.Id,
            order.PackageCodeSnapshot,
            order.PackageNameSnapshot,
            order.DurationMonthsSnapshot,
            order.AmountVnd,
            order.Status,
            order.CreatedAt,
            order.PaidAt);
    }

    public async Task<MealPlanAccessDto> GetAccessAsync(long userId, CancellationToken ct)
    {
        var latest = await repository.GetLatestAccessAsync(userId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var active = await repository.HasActiveAccessAsync(userId, now, ct);
        var trialGenerationsUsed = await mealPlanningRepository.CountTrialGenerationsAsync(userId, ct);
        return MealPlanAccessPolicy.Evaluate(latest, active, trialGenerationsUsed, now);
    }

    public async Task<PayOSWebhookAcknowledgement> HandleWebhookAsync(string payload, CancellationToken ct)
    {
        var payment = await payOS.VerifyWebhookAsync(payload, ct);
        var result = await repository.ApplyWebhookAsync(
            payment.OrderCode,
            payment.AmountVnd,
            payment.IsSuccessful,
            payment.Currency,
            payment.PaymentLinkId,
            payment.ProviderReference,
            clock.GetUtcNow().UtcDateTime,
            ct);

        if (result is PaymentWebhookApplyResult.UnknownOrder or PaymentWebhookApplyResult.AmountMismatch or PaymentWebhookApplyResult.PaymentLinkMismatch)
            logger.LogWarning("A verified PayOS webhook was ignored ({Result}) for order code {OrderCode}.", result, payment.OrderCode);

        return new PayOSWebhookAcknowledgement("00", "success");
    }

    private static string ValidateRedirectUrl(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new PaymentProviderException($"A valid PayOS {name} URL must be configured.", "PAYMENT_REDIRECT_URL_INVALID");

        return uri.ToString();
    }
}
