using EnterpriseRag.Core.Domain.Entities.Documents;

namespace EnterpriseRag.Core.Domain.Interfaces.Documents;

public interface IDocumentChunkRepository
{
    Task<IEnumerable<DocumentChunk>> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
}
