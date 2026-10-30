using EnterpriseRag.Core.Domain.Common.Errors;

namespace EnterpriseRag.Core.Domain.Common.Errors.VectorStore;

public static class VectorStoreErrors
{
    public static Error EmptyPoints =>
        new("VectorStore.EmptyPoints", "No se proporcionaron puntos vectoriales para almacenar.");

    public static Error PointNotFound(Guid pointId) =>
        new("VectorStore.PointNotFound", $"El vector con ID '{pointId}' no fue encontrado en la base de datos vectorial.");

    public static Error SearchFailed(string details) =>
        new("VectorStore.SearchFailed", $"Fallo en la búsqueda de similitud vectorial: {details}");

    public static Error UpsertFailed(string details) =>
        new("VectorStore.UpsertFailed", $"Error al insertar o actualizar puntos en la base de datos vectorial: {details}");

    public static Error DeleteFailed(string details) =>
        new("VectorStore.DeleteFailed", $"Error al eliminar vectores asociados al documento: {details}");

    public static Error CollectionCreationFailed(string collectionName, string details) =>
        new("VectorStore.CollectionCreationFailed", $"No se pudo crear o asegurar la colección '{collectionName}': {details}");

    public static Error EmptyQueryVector =>
         new("VectorStore.EmptyQueryVector", "El vector de consulta proporcionado está vacío.");

    public static Error InvalidTenantId =>
        new("VectorStore.InvalidTenantId", "El identificador del tenant proporcionado no es válido.");
}
