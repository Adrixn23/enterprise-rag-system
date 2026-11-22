using EnterpriseRag.Core.Domain.Entities.Outbox;

namespace EnterpriseRag.Core.Domain.Interfaces.Outbox;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> ClaimNextBatchAsync(int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task ScheduleRetryOrFailAsync(Guid messageId, string error, DateTimeOffset? nextAttemptAt, bool permanentFailure, CancellationToken cancellationToken = default);
}
