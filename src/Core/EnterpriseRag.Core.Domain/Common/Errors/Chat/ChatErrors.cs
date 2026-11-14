using EnterpriseRag.Core.Domain.Common.Errors;

namespace EnterpriseRag.Core.Domain.Common.Errors.Chat;

public static class ChatErrors
{
    public static Error EmptyMessage =>
        new("Chat.EmptyMessage", "El mensaje no puede estar vacío.");

    public static Error MessageTooLong(int maxLength) =>
        new("Chat.MessageTooLong", $"El mensaje excede la longitud máxima permitida de {maxLength} caracteres.");

    public static Error InvalidTenantId =>
        new("Chat.InvalidTenantId", "El identificador del tenant es inválido o no fue provisto.");

    public static Error EmptyResponse =>
        new("Chat.EmptyResponse", "El modelo de IA generó una respuesta vacía.");

    public static Error BlockedBySafety =>
        new("Chat.BlockedBySafety", "La respuesta del modelo fue bloqueada por filtros de seguridad.");

    public static Error GenerationFailed(string details) =>
        new("Chat.GenerationFailed", $"Error al generar la respuesta con el modelo de IA: {details}");
}
