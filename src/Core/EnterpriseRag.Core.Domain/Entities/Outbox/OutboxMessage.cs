namespace EnterpriseRag.Core.Domain.Entities.Outbox;

public enum OutboxMessageStatus
{
    Pending = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4
}

public static class OutboxEventTypes
{
    public const string IngestDocument = "IngestDocument";
    public const string DeleteVectors = "DeleteVectors";
}

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AggregateId { get; set; }
    public required string EventType { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 5;
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LockedUntil { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}
