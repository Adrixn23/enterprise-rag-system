using EnterpriseRag.Core.Domain.Entities.Base;

namespace EnterpriseRag.Core.Domain.Entities.Documents;

public class DocumentChunk : BaseEntity
{
    // Identificador del documento padre al que pertenece este fragmento
    public Guid DocumentId { get; set; }

    // Propiedad de navegacion hacia el documento padre en EF Core
    public Document Document { get; set; } = null!;

    // Posicion o numero de orden de este fragmento dentro del documento (0, 1, 2...)
    public int ChunkIndex { get; set; }

    // Texto limpio extraido del fragmento que se le enviara a la IA (Gemini) en el Prompt
    public required string Content { get; set; }

    // Cantidad estimada de tokens del fragmento para no exceder la ventana de contexto del LLM
    public int TokenCount { get; set; }

    // UUID del vector guardado en Qdrant. Conecta el registro de SQL con el vector en Qdrant
    public required string VectorId { get; set; }

    // Identificador del cliente/tenant para filtrado ultrarrapido por empresa sin hacer JOINs
    public required string TenantId { get; set; }
}
