using EnterpriseRag.Core.Application.DTOs.Vector;
using EnterpriseRag.Core.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseRag.Core.Application.Contracts.VectorStore
{
    public interface IVectorStoreService
    {
        // este contrato se encarga de gyuardar en qdrant
        Task<Result> UpsertPointAsync(IEnumerable<VectorPointDto> points, CancellationToken cancellationToken = default);

        // Buscar los textos más cercanos en Qdrant

        Task<Result<List<VectorSearchResultDto>>> SearchAsync(
            ReadOnlyMemory<float> QueryVector,
            Guid TenantId,
            int limit = 5,
            CancellationToken cancellationToken = default
            );

        Task<Result> DeleteByDocumentAsync(Guid documentId, Guid TenantId, CancellationToken cancellationToken = default);

    }
}
