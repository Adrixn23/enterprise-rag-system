using EnterpriseRag.Core.Application.DTOs.Vector;
using EnterpriseRag.Core.Domain.Common;

namespace EnterpriseRag.Core.Application.Contracts.VectorStore;

public interface IVectorStoreService
{
    // Almacena puntos vectoriales en Qdrant
    Task<Result> UpsertPointAsync(IEnumerable<VectorPointDto> points, CancellationToken cancellationToken = default);

    // Busca los textos mas cercanos semanticamente en Qdrant filtrado por tenant
    Task<Result<List<VectorSearchResultDto>>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        Guid tenantId,
        int limit = 5,
        CancellationToken cancellationToken = default);

    // Elimina todos los vectores asociados a un documento de un tenant
    Task<Result> DeleteByDocumentAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken = default);
}
