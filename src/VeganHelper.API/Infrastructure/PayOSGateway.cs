using System.Text.Json;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.BLL.Exceptions;
using VeganPayOSOptions = VeganHelper.BLL.DTOs.PayOSOptions;

namespace VeganHelper.API.Infrastructure;

public sealed class PayOSGateway(IOptions<VeganPayOSOptions> options, ILogger<PayOSGateway> logger) : IPayOSGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private VeganPayOSOptions Settings => options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Settings.ClientId) &&
        !string.IsNullOrWhiteSpace(Settings.ApiKey) &&
        !string.IsNullOrWhiteSpace(Settings.ChecksumKey);

    public async Task<PayOSCreatedPaymentLink> CreatePaymentLinkAsync(
        PayOSCreatePaymentLink request,
        CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var response = await client.PaymentRequests.CreateAsync(new CreatePaymentLinkRequest
            {
                OrderCode = checked((int)request.OrderCode),
                Amount = request.AmountVnd,
                Description = request.Description,
                ReturnUrl = request.ReturnUrl,
                CancelUrl = request.CancelUrl
            }, new RequestOptions<CreatePaymentLinkRequest> { CancellationToken = cancellationToken });

        if (string.IsNullOrWhiteSpace(response.PaymentLinkId) ||
            !Uri.TryCreate(response.CheckoutUrl, UriKind.Absolute, out var checkoutUri) ||
            checkoutUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("PayOS returned an invalid checkout link.");
        }

        return new PayOSCreatedPaymentLink(response.PaymentLinkId, response.CheckoutUrl);
    }

    public async Task<VerifiedPayOSPayment> VerifyWebhookAsync(
        string payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > 64_000)
            throw new PayOSWebhookVerificationException();
        if (!IsConfigured)
            throw new PaymentProviderException("The payment service is not configured yet.");

        Webhook webhook;
        try
        {
            webhook = JsonSerializer.Deserialize<Webhook>(payload, JsonOptions)
                ?? throw new JsonException("Empty webhook payload.");
        }
        catch (JsonException exception)
        {
            logger.LogWarning("PayOS sent a malformed webhook payload ({ExceptionType}).", exception.GetType().Name);
            throw new PayOSWebhookVerificationException();
        }

        try
        {
            var data = await CreateClient().Webhooks.VerifyAsync(webhook);
            return new VerifiedPayOSPayment(
                data.OrderCode,
                checked((int)data.Amount),
                string.Equals(data.Code, "00", StringComparison.Ordinal),
                data.Currency,
                data.PaymentLinkId,
                data.Reference);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("PayOS webhook verification failed ({ExceptionType}).", exception.GetType().Name);
            throw new PayOSWebhookVerificationException();
        }
    }

    private PayOSClient CreateClient()
    {
        var settings = Settings;
        if (!IsConfigured)
            throw new InvalidOperationException("PayOS credentials are not configured.");

        return new PayOSClient(settings.ClientId, settings.ApiKey, settings.ChecksumKey);
    }
}
