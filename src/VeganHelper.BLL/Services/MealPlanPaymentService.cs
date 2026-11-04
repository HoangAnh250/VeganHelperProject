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
        return ToDto(order);
    }

    public async Task<MealPlanPaymentOrderDto> VerifyOrderAsync(long userId, long orderId, CancellationToken ct)
    {
        var order = await repository.GetOrderForUserAsync(orderId, userId, ct)
            ?? throw new NotFoundException("Payment order was not found.");

        if (!string.Equals(order.Status, "pending", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(order.PaymentLinkId))
            return ToDto(order);

        var providerStatus = await payOS.GetPaymentStatusAsync(order.OrderCode, ct);
        var paymentLinkMatches = string.Equals(order.PaymentLinkId, providerStatus.PaymentLinkId, StringComparison.Ordinal);
        var orderMatches = providerStatus.OrderCode == order.OrderCode;
        var amountMatches = providerStatus.AmountVnd == order.AmountVnd;

        if (!paymentLinkMatches || !orderMatches || !amountMatches)
        {
            logger.LogWarning(
                "PayOS status check did not match local payment order {OrderId} (link={LinkMatches}, order={OrderMatches}, amount={AmountMatches}).",
                order.Id,
                paymentLinkMatches,
                orderMatches,
                amountMatches);
            return ToDto(order);
        }

        var status = providerStatus.Status;
        var isPaid = string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase);
        var isFullyPaid = isPaid && providerStatus.AmountPaidVnd == order.AmountVnd && providerStatus.AmountRemainingVnd == 0;
        var isTerminalFailure = string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Expired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase);

        if (isFullyPaid || isTerminalFailure)
        {
            var applyResult = await repository.ApplyWebhookAsync(
                order.OrderCode,
                order.AmountVnd,
                isFullyPaid,
                "VND",
                providerStatus.PaymentLinkId,
                providerStatus.ProviderReference,
                clock.GetUtcNow().UtcDateTime,
                ct);

            if (applyResult is PaymentWebhookApplyResult.UnknownOrder or PaymentWebhookApplyResult.AmountMismatch or PaymentWebhookApplyResult.PaymentLinkMismatch)
                logger.LogWarning("PayOS status was ignored ({Result}) for local payment order {OrderId}.", applyResult, order.Id);

            order = await repository.GetOrderForUserAsync(orderId, userId, ct)
                ?? throw new NotFoundException("Payment order was not found.");
        }
        else if (isPaid)
        {
            logger.LogWarning("PayOS reports payment link {OrderId} as paid but its paid amount is incomplete.", order.Id);
        }

        return ToDto(order);
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

    private static MealPlanPaymentOrderDto ToDto(VeganHelper.DAL.Entities.MealPlanPaymentOrder order) => new(
        order.Id,
        order.PackageCodeSnapshot,
        order.PackageNameSnapshot,
        order.DurationMonthsSnapshot,
        order.AmountVnd,
        order.Status,
        order.CreatedAt,
        order.PaidAt);
}
