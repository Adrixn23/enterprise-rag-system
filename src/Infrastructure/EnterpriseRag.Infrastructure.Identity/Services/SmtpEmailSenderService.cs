namespace EnterpriseRag.Infrastructure.Identity.Services;

using System.Net;
using System.Net.Mail;
using EnterpriseRag.Core.Application.Contracts.Email;
using EnterpriseRag.Core.Domain.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class SmtpEmailSenderService : IEmailSenderService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailSenderService> _logger;

    public SmtpEmailSenderService(
        IOptions<EmailSettings> options,
        ILogger<SmtpEmailSenderService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Enviando correo de confirmación a {Email} vía SMTP.", toEmail);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = true,
            Timeout = 15000
        };

        if (!string.IsNullOrWhiteSpace(_settings.UserName))
        {
            client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);
        }

        var safeLink = WebUtility.HtmlEncode(confirmationLink);

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = "Confirma tu cuenta - Enterprise RAG System",
            Body = $@"
                <h2>Bienvenido a Enterprise RAG System</h2>
                <p>Por favor confirma tu cuenta haciendo clic en el siguiente enlace:</p>
                <p><a href='{safeLink}'>Completar Verificación de Cuenta</a></p>
                <p>Si no solicitaste esta cuenta, puedes ignorar este mensaje.</p>",
            IsBodyHtml = true
        };

        message.To.Add(toEmail);

        await client.SendMailAsync(message, cancellationToken);
    }
}
