namespace VeganHelper.BLL.Exceptions;

public sealed class PayOSWebhookVerificationException()
    : Exception("The PayOS webhook signature is invalid.");
