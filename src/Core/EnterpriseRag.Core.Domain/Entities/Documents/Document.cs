using EnterpriseRag.Core.Domain.Entities.Base;
using EnterpriseRag.Core.Domain.Enums;

namespace EnterpriseRag.Core.Domain.Entities.Documents;

public class Document : BaseEntity
{
    // Nombre original del archivo cargado por el usuario (ej: Manual_Operaciones.pdf)
    public required string FileName { get; set; }

    // Tipo MIME del archivo (ej: application/pdf, text/plain) para saber que extractor usar
    public required string ContentType { get; set; }

    // Tamaño del archivo en bytes para validar limites y cuotas de almacenamiento
    public long FileSizeBytes { get; set; }

    // Ruta o URI donde esta guardado el archivo fisico (nunca guardamos binarios en SQL)
    public required string StoragePath { get; set; }

    // Identificador de la empresa u organizacion a la que pertenece el documento (Multitenancy)
    public required string TenantId { get; set; }

    // Estado actual del procesamiento asincrono (Pending, Processing, Completed, Failed)
    public DocumentProcessingStatus Status { get; set; } = DocumentProcessingStatus.Pending;

    // Mensaje de error explicativo en caso de que el procesamiento haya fallado
    public string? ErrorMessage { get; set; }

    // Coleccion de fragmentos (chunks) en los que fue dividido este documento
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
