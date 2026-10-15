namespace EnterpriseRag.Infrastructure.Persistence.Repositories.Documents;

using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Interfaces.Documents;
using EnterpriseRag.Infrastructure.Persistence.Context;
using EnterpriseRag.Infrastructure.Persistence.Repositories.GenericRepository;
using Microsoft.EntityFrameworkCore;

public class DocumentChunkRepository : GenericRepository<DocumentChunk, Guid>, IDocumentChunkRepository
{
    public DocumentChunkRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<DocumentChunk>> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(chunks, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await _dbSet
            .Where(c => c.DocumentId == documentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsDeleted, true)
                .SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);
    }
}
