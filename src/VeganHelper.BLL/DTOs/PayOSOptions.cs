namespace VeganHelper.BLL.DTOs;

public sealed class PayOSOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "http://localhost:3000/meal-plans/payment/success";
    public string CancelUrl { get; set; } = "http://localhost:3000/meal-plans/payment/cancel";
}
