using EnterpriseRag.Core.Domain.Common.Errors;

namespace EnterpriseRag.Core.Domain.Common.Errors.Embeddings;

public static class EmbeddingErrors
{
    public static Error EmptyText =>
        new("Embedding.EmptyText", "El texto proporcionado para generar el embedding no puede estar vacío.");

    public static Error EmptyList =>
        new("Embedding.EmptyList", "La lista de textos para generar embeddings no contiene elementos.");

    public static Error ApiError(string statusCode, string details) =>
        new("Embedding.ApiError", $"Error devuelto por la API de Embeddings ({statusCode}): {details}");

    public static Error EmptyResponse =>
        new("Embedding.EmptyResponse", "El proveedor de IA no retornó vectores de embedding válidos.");

    public static Error DimensionMismatch(int expected, int actual) =>
        new("Embedding.DimensionMismatch", $"La dimensión del vector recibida ({actual}) no coincide con la esperada ({expected}).");

    public static Error OperationCancelled =>
        new("Embedding.Cancelled", "La generación del embedding fue cancelada.");

    public static Error Unexpected(string message) =>
        new("Embedding.Unexpected", $"Ocurrió una excepción no controlada en el servicio de embeddings: {message}");
}
