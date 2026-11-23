using EnterpriseRag.Core.Domain.Entities.Outbox;
using EnterpriseRag.Core.Domain.Interfaces.Outbox;
using EnterpriseRag.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseRag.Infrastructure.Persistence.Repositories.Outbox;

public class OutboxRepository : IOutboxRepository
{
    private readonly ApplicationDbContext _context;

    public OutboxRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _context.OutboxMessages.AddAsync(message, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimNextBatchAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var leaseExpiry = now.Add(leaseDuration);
        var claimed = new List<OutboxMessage>();

        var candidateIds = await _context.OutboxMessages
            .AsNoTracking()
            .Where(m =>
                (m.Status == OutboxMessageStatus.Pending && m.NextAttemptAt <= now) ||
                (m.Status == OutboxMessageStatus.Processing && m.LockedUntil != null && m.LockedUntil < now))
            .OrderBy(m => m.NextAttemptAt)
            .ThenBy(m => m.CreatedAt)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var id in candidateIds)
        {
            // Reclamo atómico optimista: solo una instancia puede ganar el UPDATE
            var rows = await _context.OutboxMessages
                .Where(m => m.Id == id &&
                    ((m.Status == OutboxMessageStatus.Pending && m.NextAttemptAt <= now) ||
                     (m.Status == OutboxMessageStatus.Processing && m.LockedUntil != null && m.LockedUntil < now)))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, OutboxMessageStatus.Processing)
                    .SetProperty(m => m.LockedUntil, leaseExpiry),
                    cancellationToken);

            if (rows == 1)
            {
                var entity = await _context.OutboxMessages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

                if (entity is not null)
                {
                    claimed.Add(entity);
                }
            }
        }

        return claimed;
    }

    public async Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await _context.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxMessageStatus.Processed)
                .SetProperty(m => m.ProcessedAt, now)
                .SetProperty(m => m.LockedUntil, (DateTimeOffset?)null)
                .SetProperty(m => m.LastError, (string?)null),
                cancellationToken);
    }

    public async Task ScheduleRetryOrFailAsync(
        Guid messageId,
        string error,
        DateTimeOffset? nextAttemptAt,
        bool permanentFailure,
        CancellationToken cancellationToken = default)
    {
        var truncatedError = error.Length > 2000 ? error[..2000] : error;
        var nextStatus = permanentFailure ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;

        await _context.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, nextStatus)
                .SetProperty(m => m.RetryCount, m => m.RetryCount + 1)
                .SetProperty(m => m.NextAttemptAt, nextAttemptAt ?? DateTimeOffset.UtcNow)
                .SetProperty(m => m.LockedUntil, (DateTimeOffset?)null)
                .SetProperty(m => m.LastError, truncatedError),
                cancellationToken);
    }
}
