using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Entities.Outbox;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Interfaces.GenericRepository;

namespace EnterpriseRag.Core.Domain.Interfaces.Documents;

public interface IDocumentRepository : IGenericRepository<Document, Guid>
{
    Task<IEnumerable<Document>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Document> Items, int TotalCount)> GetPagedByTenantIdAsync(Guid tenantId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Document?> GetByHashAsync(Guid tenantId, string fileHash, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid id, Guid tenantId, DocumentProcessingStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task AddWithOutboxAsync(Document document, OutboxMessage outboxMessage, CancellationToken cancellationToken = default);
    Task CompleteIngestionAsync(Guid documentId, Guid tenantId, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    // Consulta en lote optimizada (Batch) para evitar el antipatrón N+1
    Task<IReadOnlyDictionary<Guid, string>> GetFileNamesByIdsAsync(
        IEnumerable<Guid> documentIds,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
