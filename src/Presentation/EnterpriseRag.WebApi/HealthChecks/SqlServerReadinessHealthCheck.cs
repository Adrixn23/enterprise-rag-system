using EnterpriseRag.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnterpriseRag.WebApi.HealthChecks;

public sealed class SqlServerReadinessHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _dbContext;

    public SqlServerReadinessHealthCheck(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

            var canConnect = await _dbContext.Database.CanConnectAsync(timeoutCts.Token);
            return canConnect
                ? HealthCheckResult.Healthy("SQL Server disponible.")
                : HealthCheckResult.Unhealthy("SQL Server no disponible.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // No exponer cadenas de conexión, servidor ni detalles internos en el endpoint de salud
            return HealthCheckResult.Unhealthy("SQL Server no disponible.");
        }
    }
}
