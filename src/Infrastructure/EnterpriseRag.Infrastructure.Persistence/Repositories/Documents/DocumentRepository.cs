namespace EnterpriseRag.Infrastructure.Persistence.Repositories.Documents;

using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Interfaces.Documents;
using EnterpriseRag.Infrastructure.Persistence.Context;
using EnterpriseRag.Infrastructure.Persistence.Repositories.GenericRepository;
using Microsoft.EntityFrameworkCore;

public class DocumentRepository : GenericRepository<Document, Guid>, IDocumentRepository
{
    public DocumentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Document>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(Guid id, DocumentProcessingStatus status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        await _dbSet
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, status)
                .SetProperty(d => d.ErrorMessage, errorMessage)
                .SetProperty(d => d.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);
    }
}
