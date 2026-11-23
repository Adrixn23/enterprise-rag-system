using EnterpriseRag.Core.Application.Contracts.Documents;
using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Domain.Entities.Outbox;
using EnterpriseRag.Core.Domain.Interfaces.Outbox;

namespace EnterpriseRag.WebApi.Workers;

/// <summary>
/// Worker en segundo plano que procesa la cola persistente Outbox (IngestDocument / DeleteVectors)
/// con lease atómico en SQL Server para tolerancia a reinicios y ejecución segura en múltiples réplicas.
/// </summary>
public sealed class DocumentIngestionWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentIngestionWorker> _logger;

    public DocumentIngestionWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentIngestionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en el ciclo de DocumentIngestionWorker.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
        var vectorStoreService = scope.ServiceProvider.GetRequiredService<IVectorStoreService>();

        var messages = await outboxRepository.ClaimNextBatchAsync(batchSize: 5, LeaseDuration, cancellationToken);
        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = message.EventType switch
                {
                    OutboxEventTypes.IngestDocument =>
                        await documentService.ProcessPendingDocumentAsync(message.AggregateId, message.TenantId, cancellationToken),
                    OutboxEventTypes.DeleteVectors =>
                        await vectorStoreService.DeleteByDocumentAsync(message.AggregateId, message.TenantId, cancellationToken),
                    _ => EnterpriseRag.Core.Domain.Common.Result.Success()
                };

                if (result.IsSuccess)
                {
                    await outboxRepository.MarkProcessedAsync(message.Id, cancellationToken);
                }
                else
                {
                    var error = result.Errors.FirstOrDefault()?.Description ?? "Error al procesar mensaje Outbox.";
                    await HandleFailureAsync(outboxRepository, message, error, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await HandleFailureAsync(outboxRepository, message, ex.Message, cancellationToken);
            }
        }
    }

    private static async Task HandleFailureAsync(
        IOutboxRepository outboxRepository,
        OutboxMessage message,
        string error,
        CancellationToken cancellationToken)
    {
        var nextRetryCount = message.RetryCount + 1;
        var permanentFailure = nextRetryCount >= message.MaxRetries;
        var backoffSeconds = Math.Min(300, Math.Pow(2, nextRetryCount) * 5);
        var nextAttemptAt = permanentFailure
            ? (DateTimeOffset?)null
            : DateTimeOffset.UtcNow.AddSeconds(backoffSeconds);

        await outboxRepository.ScheduleRetryOrFailAsync(
            message.Id,
            error,
            nextAttemptAt,
            permanentFailure,
            cancellationToken);
    }
}
