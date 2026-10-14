namespace VeganHelper.BLL.DTOs;

public sealed record EmailMessage(
    string ToEmail,
    string Subject,
    string PlainTextBody,
    string HtmlBody);

public sealed record GoogleIdentityInfo(
    string Subject,
    string Email,
    bool EmailVerified,
    string? DisplayName,
    string? PictureUrl);

public sealed class GoogleOptions
{
    public string ClientId { get; set; } = string.Empty;
}

public sealed class SendGridOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "VeganHelper";
}
