namespace EnterpriseRag.Core.Application.Contracts.Email;

public interface IEmailSenderService
{
    Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, CancellationToken cancellationToken = default);
}
