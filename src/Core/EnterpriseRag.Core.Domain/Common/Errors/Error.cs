namespace EnterpriseRag.Core.Domain.Common.Errors;

public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "El valor proporcionado es nulo.");
    public static readonly Error NotFound = new("Error.NotFound", "El registro solicitado no fue encontrado.");
}
