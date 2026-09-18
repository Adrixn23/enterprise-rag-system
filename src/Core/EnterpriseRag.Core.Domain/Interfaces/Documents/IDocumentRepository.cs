using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Enums;

namespace EnterpriseRag.Core.Domain.Interfaces.Documents;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Document>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(Document document, CancellationToken cancellationToken = default);
    Task UpdateAsync(Document document, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid id, DocumentProcessingStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
