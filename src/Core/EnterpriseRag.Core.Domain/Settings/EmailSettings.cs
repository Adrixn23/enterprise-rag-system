using System.ComponentModel.DataAnnotations;

namespace EnterpriseRag.Core.Domain.Settings;

public class EmailSettings : IValidatableObject
{
    public const string SectionName = "EmailSettings";

    public string Host { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "El puerto SMTP debe estar entre 1 y 65535.")]
    public int Port { get; set; } = 587;

    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Enterprise RAG System";

    [Required(ErrorMessage = "PublicBaseUrl es obligatorio para construir enlaces de confirmación.")]
    public string PublicBaseUrl { get; set; } = "http://localhost:5235";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(PublicBaseUrl)
            || !Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            yield return new ValidationResult(
                "PublicBaseUrl debe ser una URL absoluta HTTP o HTTPS válida.",
                [nameof(PublicBaseUrl)]);
        }
    }
}
