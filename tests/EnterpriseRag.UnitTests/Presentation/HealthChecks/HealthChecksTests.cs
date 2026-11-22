using EnterpriseRag.Infrastructure.Persistence.Context;
using EnterpriseRag.WebApi.HealthChecks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qdrant.Client;
using Xunit;

namespace EnterpriseRag.UnitTests.Presentation.HealthChecks;

public class HealthChecksTests
{
    [Fact]
    public async Task SqlServerReadinessHealthCheck_WhenDatabaseIsUnreachable_ShouldReturnUnhealthyWithoutExposingSecrets()
    {
        const string secretConnectionString = "Server=prod-sql.internal;Database=EnterpriseRag;User Id=sa;Password=SuperSecretPassword123!;";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(secretConnectionString)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        var sut = new SqlServerReadinessHealthCheck(dbContext);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("SQL Server no disponible.");
        result.Description.Should().NotContain("SuperSecretPassword123!");
        result.Description.Should().NotContain("prod-sql.internal");
        result.Exception.Should().BeNull();
    }

    [Fact]
    public async Task QdrantReadinessHealthCheck_WhenQdrantIsUnreachable_ShouldReturnUnhealthyWithoutExposingSecrets()
    {
        var client = new QdrantClient("nonexistent-qdrant-host.local", 6334, apiKey: "qdrant-secret-key");
        var sut = new QdrantReadinessHealthCheck(client);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Qdrant no disponible.");
        result.Description.Should().NotContain("qdrant-secret-key");
        result.Description.Should().NotContain("nonexistent-qdrant-host.local");
        result.Exception.Should().BeNull();
    }
}
