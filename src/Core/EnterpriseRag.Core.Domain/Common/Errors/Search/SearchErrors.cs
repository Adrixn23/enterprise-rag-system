using EnterpriseRag.Core.Domain.Common.Errors;

namespace EnterpriseRag.Core.Domain.Common.Errors.Search;

public static class SearchErrors
{
    public static Error EmptyQuery =>
        new("Search.EmptyQuery", "El término de búsqueda no puede estar vacío.");

    public static Error QueryTooShort(int minLength) =>
        new("Search.QueryTooShort", $"El término de búsqueda debe contener al menos {minLength} caracteres.");

    public static Error InvalidLimit =>
        new("Search.InvalidLimit", "El límite de resultados debe ser un valor positivo entre 1 y 50.");

    public static Error InvalidMinScore =>
        new("Search.InvalidMinScore", "El umbral de similitud (MinScore) debe ser un valor entre 0.0 y 1.0.");

    public static Error InvalidTenantId =>
        new("Search.InvalidTenantId", "El identificador del tenant es inválido o no fue provisto.");

    public static Error NoResultsFound =>
        new("Search.NoResultsFound", "No se encontraron fragmentos relevantes que coincidan con la consulta.");
}
