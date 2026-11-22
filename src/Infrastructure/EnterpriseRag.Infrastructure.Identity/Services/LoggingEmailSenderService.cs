namespace EnterpriseRag.Infrastructure.Identity.Services;

using EnterpriseRag.Core.Application.Contracts.Email;
using Microsoft.Extensions.Logging;

public class LoggingEmailSenderService : IEmailSenderService
{
    private readonly ILogger<LoggingEmailSenderService> _logger;

    public LoggingEmailSenderService(ILogger<LoggingEmailSenderService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailConfirmationAsync(string toEmail, string confirmationLink, CancellationToken cancellationToken = default)
    {
        // En Development: NUNCA registrar el token ni el enlace completo que contenga el token
        // para prevenir filtraciones inadvertidas en archivos de log o consolas.
        _logger.LogInformation(
            "==== [DEV EMAIL SENDER] Notificación de confirmación de correo enviada a '{Email}'. (El token criptográfico está resguardado por seguridad).",
            toEmail);

        return Task.CompletedTask;
    }
}
