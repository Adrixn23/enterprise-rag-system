using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qdrant.Client;

namespace EnterpriseRag.WebApi.HealthChecks;

public sealed class QdrantReadinessHealthCheck : IHealthCheck
{
    private readonly QdrantClient _qdrantClient;

    public QdrantReadinessHealthCheck(QdrantClient qdrantClient)
    {
        _qdrantClient = qdrantClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

            await _qdrantClient.ListCollectionsAsync(timeoutCts.Token);
            return HealthCheckResult.Healthy("Qdrant disponible.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // No exponer host, puerto, API Key ni stack trace en el endpoint de salud
            return HealthCheckResult.Unhealthy("Qdrant no disponible.");
        }
    }
}
