using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Interfaces.GenericRepository;

namespace EnterpriseRag.Core.Domain.Interfaces.Documents;

public interface IDocumentRepository : IGenericRepository<Document, Guid>
{
    Task<IEnumerable<Document>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid id, Guid tenantId, DocumentProcessingStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);

    // Consulta en lote optimizada (Batch) para evitar el antipatrón N+1
    Task<IReadOnlyDictionary<Guid, string>> GetFileNamesByIdsAsync(
        IEnumerable<Guid> documentIds,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
