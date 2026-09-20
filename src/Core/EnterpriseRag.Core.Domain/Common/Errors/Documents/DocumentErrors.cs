namespace EnterpriseRag.Core.Domain.Common.Errors.Documents;

public static class DocumentErrors
{
    public static Error EmptyFile =>
        new("Document.EmptyFile", "El archivo subido está vacío.");

    public static Error UnsupportedFormat =>
        new("Document.UnsupportedFormat", "El formato de archivo no es soportado.");

    public static Error NotFound =>
        new("Document.NotFound", "El documento solicitado no fue encontrado.");

    public static Error ExtractionFailed(string details) =>
        new("Document.ExtractionFailed", $"Fallo al extraer el texto: {details}");

    public static Error StorageFailed(string details) =>
        new("Document.StorageFailed", $"Fallo al almacenar el archivo: {details}");
}
