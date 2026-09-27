using VeganHelper.BLL.DTOs;

namespace VeganHelper.BLL.Contracts;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
