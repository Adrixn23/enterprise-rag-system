using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Interfaces.GenericRepository;

namespace EnterpriseRag.Core.Domain.Interfaces.Documents;

public interface IDocumentChunkRepository : IGenericRepository<DocumentChunk, Guid>
{
    Task<IEnumerable<DocumentChunk>> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
}
