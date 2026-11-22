namespace EnterpriseRag.Infrastructure.Persistence.Repositories.Documents;

using System.Diagnostics;
using EnterpriseRag.Core.Domain.Observability;
using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Entities.Outbox;
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

    public async Task<IEnumerable<Document>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync(cancellationToken);
        }
        finally
        {
            RagTelemetry.SqlDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("operation", "get_by_tenant"));
        }
    }

    public async Task<(IReadOnlyList<Document> Items, int TotalCount)> GetPagedByTenantIdAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var baseQuery = _dbSet
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId);

            var totalCount = await baseQuery.CountAsync(cancellationToken);
            if (totalCount == 0)
            {
                return ([], 0);
            }

            var items = await baseQuery
                .OrderByDescending(d => d.CreatedAt)
                .ThenBy(d => d.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }
        finally
        {
            RagTelemetry.SqlDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("operation", "get_paged_by_tenant"));
        }
    }

    public async Task<Document?> GetByHashAsync(Guid tenantId, string fileHash, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(fileHash))
        {
            return null;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.FileHash == fileHash, cancellationToken);
        }
        finally
        {
            RagTelemetry.SqlDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("operation", "get_by_hash"));
        }
    }

    public async Task UpdateStatusAsync(Guid id, Guid tenantId, DocumentProcessingStatus status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty)
        {
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _dbSet
                .Where(d => d.Id == id && d.TenantId == tenantId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, status)
                    .SetProperty(d => d.ErrorMessage, errorMessage)
                    .SetProperty(d => d.UpdatedAt, DateTimeOffset.UtcNow),
                    cancellationToken);
        }
        finally
        {
            RagTelemetry.SqlDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("operation", "update_status"));
        }
    }

    public async Task AddWithOutboxAsync(
        Document document,
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            await _context.Documents.AddAsync(document, cancellationToken);
            await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });
    }

    public async Task CompleteIngestionAsync(
        Guid documentId,
        Guid tenantId,
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

            if (chunks.Count > 0)
            {
                await _context.DocumentChunks.AddRangeAsync(chunks, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await _dbSet
                .Where(d => d.Id == documentId && d.TenantId == tenantId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, DocumentProcessingStatus.Completed)
                    .SetProperty(d => d.ErrorMessage, (string?)null)
                    .SetProperty(d => d.UpdatedAt, DateTimeOffset.UtcNow),
                    cancellationToken);

            await tx.CommitAsync(cancellationToken);
        });
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetFileNamesByIdsAsync(
        IEnumerable<Guid> documentIds,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var idList = documentIds?.Distinct().ToList();
        if (idList == null || idList.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var sw = Stopwatch.StartNew();
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId && idList.Contains(d.Id))
                .Select(d => new { d.Id, d.FileName })
                .ToDictionaryAsync(d => d.Id, d => d.FileName, cancellationToken);
        }
        finally
        {
            RagTelemetry.SqlDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("operation", "get_filenames"));
        }
    }
}
