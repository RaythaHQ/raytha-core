using App.Domain.Common;

namespace App.Application.Common.Interfaces;

public interface IEmailer
{
    Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
