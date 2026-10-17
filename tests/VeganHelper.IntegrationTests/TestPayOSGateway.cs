using System.Text.Json;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.BLL.Exceptions;

namespace VeganHelper.IntegrationTests;

/// <summary>
/// Deterministic test double. It deliberately does not make network requests or
/// claim to validate PayOS signatures; signature verification belongs to the SDK adapter.
/// </summary>
public sealed class TestPayOSGateway : IPayOSGateway
{
    public const string TestSignature = "integration-test-signature";
    public bool IsConfigured => true;

    public Task<PayOSCreatedPaymentLink> CreatePaymentLinkAsync(
        PayOSCreatePaymentLink request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PayOSCreatedPaymentLink(
            $"test-link-{request.OrderCode}",
            $"https://payments.example.invalid/checkout/{request.OrderCode}"));
    }

    public Task<VerifiedPayOSPayment> VerifyWebhookAsync(string payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (!root.TryGetProperty("signature", out var signature) ||
            signature.GetString() != TestSignature)
        {
            throw new PayOSWebhookVerificationException();
        }

        return Task.FromResult(new VerifiedPayOSPayment(
            root.GetProperty("orderCode").GetInt64(),
            root.GetProperty("amountVnd").GetInt32(),
            root.GetProperty("isSuccessful").GetBoolean(),
            root.GetProperty("currency").GetString() ?? string.Empty,
            root.TryGetProperty("paymentLinkId", out var paymentLinkId) ? paymentLinkId.GetString() : null,
            root.TryGetProperty("providerReference", out var providerReference) ? providerReference.GetString() : null));
    }
}
