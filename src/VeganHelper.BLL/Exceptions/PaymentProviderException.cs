namespace VeganHelper.BLL.Exceptions;

public sealed class PaymentProviderException(string message, string errorCode = "PAYMENT_PROVIDER_UNAVAILABLE")
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
