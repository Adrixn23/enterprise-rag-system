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
        _logger.LogInformation(
            "==== [SERVICIO DE CORREO - CONFIRMACIÓN] ====\nDestinatario: {Email}\nEnlace de Verificación: {Link}\nPara producción, configure un proveedor SMTP real en IEmailSenderService.",
            toEmail,
            confirmationLink);

        return Task.CompletedTask;
    }
}
