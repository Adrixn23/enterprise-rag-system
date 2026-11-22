using System.Net.Mail;
using EnterpriseRag.Core.Domain.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EnterpriseRag.Infrastructure.Identity.Configuration;

public sealed class EmailSettingsValidator : IValidateOptions<EmailSettings>
{
    private readonly IHostEnvironment _environment;

    public EmailSettingsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, EmailSettings options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl)
            || !Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add("EmailSettings:PublicBaseUrl debe ser una URL absoluta HTTP o HTTPS válida.");
        }
        else if (!_environment.IsDevelopment())
        {
            if (baseUri.Scheme != Uri.UriSchemeHttps)
            {
                failures.Add("EmailSettings:PublicBaseUrl debe usar HTTPS en entornos que no son de desarrollo.");
            }

            if (baseUri.IsLoopback)
            {
                failures.Add("EmailSettings:PublicBaseUrl no puede apuntar a localhost/loopback en entornos que no son de desarrollo.");
            }
        }

        if (!_environment.IsDevelopment())
        {
            if (string.IsNullOrWhiteSpace(options.Host))
            {
                failures.Add("EmailSettings:Host es obligatorio en entornos que no son de desarrollo.");
            }

            if (options.Port is < 1 or > 65535)
            {
                failures.Add("EmailSettings:Port debe estar entre 1 y 65535.");
            }

            if (string.IsNullOrWhiteSpace(options.FromEmail) || !MailAddress.TryCreate(options.FromEmail, out _))
            {
                failures.Add("EmailSettings:FromEmail debe ser una dirección de correo válida en entornos que no son de desarrollo.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
