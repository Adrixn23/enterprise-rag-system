using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Interfaces.GenericRepository;

namespace EnterpriseRag.Core.Domain.Interfaces.Documents;

public interface IDocumentRepository : IGenericRepository<Document, Guid>
{
    Task<IEnumerable<Document>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid id, DocumentProcessingStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);
}
