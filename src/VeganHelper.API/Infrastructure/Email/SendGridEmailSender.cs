using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;

namespace VeganHelper.API.Infrastructure.Email;

public sealed class SendGridEmailSender(
    IOptions<SendGridOptions> options,
    IHostEnvironment environment,
    ILogger<SendGridEmailSender> logger) : IEmailSender
{
    private readonly SendGridOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("SendGrid:ApiKey and SendGrid:FromEmail must be configured.");

            logger.LogWarning(
                "Development email fallback. To: {ToEmail}; Subject: {Subject}; Body: {Body}",
                message.ToEmail,
                message.Subject,
                message.PlainTextBody);
            return;
        }

        var client = new SendGridClient(_options.ApiKey);
        var from = new EmailAddress(_options.FromEmail, _options.FromName);
        var to = new EmailAddress(message.ToEmail);
        var mail = MailHelper.CreateSingleEmail(
            from,
            to,
            message.Subject,
            message.PlainTextBody,
            message.HtmlBody);

        var response = await client.SendEmailAsync(mail, cancellationToken);
        var statusCode = (int)response.StatusCode;
        if (statusCode is < 200 or >= 300)
        {
            var details = response.Body is null
                ? string.Empty
                : await response.Body.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"SendGrid rejected the email ({statusCode}): {details}");
        }
    }
}
