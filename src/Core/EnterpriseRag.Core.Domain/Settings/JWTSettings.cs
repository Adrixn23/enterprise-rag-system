using System.ComponentModel.DataAnnotations;
using System.Text;

namespace EnterpriseRag.Core.Domain.Settings;

public class JWTSettings : IValidatableObject
{
    public const string SectionName = "JWTSettings";

    [Required(ErrorMessage = "La clave JWT (Key) es obligatoria.")]
    public string Key { get; set; } = string.Empty;

    [Required(ErrorMessage = "El emisor (Issuer) del JWT es obligatorio.")]
    public string Issuer { get; set; } = string.Empty;

    [Required(ErrorMessage = "La audiencia (Audience) del JWT es obligatoria.")]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 1440, ErrorMessage = "La duración del token debe ser entre 1 minuto y 1440 minutos (24 horas).")]
    public double DurationInMinutes { get; set; } = 60;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            yield return new ValidationResult(
                "La clave JWT no puede estar vacía ni contener solo espacios.",
                [nameof(Key)]);
            yield break;
        }

        // Validación de entropía criptográfica: HMAC-SHA256 requiere una clave simétrica de al menos 256 bits (32 bytes UTF-8)
        var keyBytes = Encoding.UTF8.GetByteCount(Key);
        if (keyBytes < 32)
        {
            yield return new ValidationResult(
                $"La clave JWT es insuficiente: contiene {keyBytes} bytes ({keyBytes * 8} bits). Se requieren al menos 32 bytes (256 bits) de entropía para HMAC-SHA256.",
                [nameof(Key)]);
        }
    }
}
