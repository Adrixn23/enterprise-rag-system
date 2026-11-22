using EnterpriseRag.Core.Application.DTOs.Vector;
using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.Persistence.Context;
using EnterpriseRag.Infrastructure.Persistence.Repositories.Documents;
using EnterpriseRag.Infrastructure.VectorStore.VectorStoreService;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Xunit;

namespace EnterpriseRag.IntegrationTests;

[Collection("SqlAndQdrantIntegration")]
public sealed class MultiTenantSqlAndQdrantIntegrationTests : IAsyncLifetime
{
    private const string ExpectedDatabaseName = "EnterpriseRagIntegrationTestsDb";
    private const string ProtectedProductionCollection = "enterprise-documents";

    private readonly Guid _tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _tenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly string _sqlConnectionString;
    private readonly string _qdrantCollectionName;
    private readonly QdrantClient _qdrantClient;
    private readonly QdrantVectorStoreService _vectorStoreService;

    private DbContextOptions<ApplicationDbContext> _dbOptions = null!;

    public MultiTenantSqlAndQdrantIntegrationTests()
    {
        var rawConnection = Environment.GetEnvironmentVariable("INTEGRATION_SQL_CONNECTION")
            ?? "Server=localhost,1433;Database=EnterpriseRagIntegrationTestsDb;User Id=sa;Password=YourStrong@Pass123!;TrustServerCertificate=True;MultipleActiveResultSets=true;";

        var sqlBuilder = new SqlConnectionStringBuilder(rawConnection);
        if (!string.Equals(sqlBuilder.InitialCatalog, ExpectedDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SEGURIDAD: Las pruebas de integración solo pueden ejecutarse contra '{ExpectedDatabaseName}', nunca contra '{sqlBuilder.InitialCatalog}'.");
        }

        _sqlConnectionString = sqlBuilder.ConnectionString;

        _qdrantCollectionName = $"enterprise-it-{Guid.NewGuid():N}";
        if (string.Equals(_qdrantCollectionName, ProtectedProductionCollection, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SEGURIDAD: Nombre de colección temporal inválido.");
        }

        var qdrantHost = Environment.GetEnvironmentVariable("INTEGRATION_QDRANT_HOST") ?? "localhost";
        var qdrantPort = int.TryParse(Environment.GetEnvironmentVariable("INTEGRATION_QDRANT_PORT"), out var p) ? p : 6334;

        _qdrantClient = new QdrantClient(qdrantHost, qdrantPort);
        _vectorStoreService = new QdrantVectorStoreService(
            _qdrantClient,
            Options.Create(new QdrantSettings
            {
                Host = qdrantHost,
                Port = qdrantPort,
                CollectionName = _qdrantCollectionName
            }),
            NullLogger<QdrantVectorStoreService>.Instance);
    }

    public async Task InitializeAsync()
    {
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_sqlConnectionString)
            .Options;

        await using var db = CreateDbContext();
        AssertDatabaseIdentity(db);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (_qdrantCollectionName.StartsWith("enterprise-it-", StringComparison.Ordinal)
                && !string.Equals(_qdrantCollectionName, ProtectedProductionCollection, StringComparison.OrdinalIgnoreCase))
            {
                var exists = await _qdrantClient.CollectionExistsAsync(_qdrantCollectionName);
                if (exists)
                {
                    await _qdrantClient.DeleteCollectionAsync(_qdrantCollectionName);
                }
            }
        }
        finally
        {
            _qdrantClient.Dispose();

            await using var db = CreateDbContext();
            AssertDatabaseIdentity(db);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private ApplicationDbContext CreateDbContext() => new(_dbOptions);

    private static void AssertDatabaseIdentity(ApplicationDbContext db)
    {
        var dbName = db.Database.GetDbConnection().Database;
        if (!string.Equals(dbName, ExpectedDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"ABORTADO: Se intentó ejecutar una operación destructiva sobre '{dbName}' en lugar de '{ExpectedDatabaseName}'.");
        }
    }

    private static ReadOnlyMemory<float> CreateDeterministicVector(float primaryValue)
    {
        var values = new float[768];
        values[0] = primaryValue;
        values[1] = 0.25f;
        return new ReadOnlyMemory<float>(values);
    }

    private async Task<(Document DocA, Document DocB)> SeedBothTenantsAsync()
    {
        var docA = new Document
        {
            Id = Guid.NewGuid(),
            FileName = "politica-tenant-a.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            StoragePath = "tenant-a/politica-tenant-a.pdf",
            TenantId = _tenantA,
            Status = DocumentProcessingStatus.Completed
        };

        var docB = new Document
        {
            Id = Guid.NewGuid(),
            FileName = "contrato-tenant-b.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 2048,
            StoragePath = "tenant-b/contrato-tenant-b.pdf",
            TenantId = _tenantB,
            Status = DocumentProcessingStatus.Completed
        };

        var pointAId = Guid.NewGuid();
        var pointBId = Guid.NewGuid();

        docA.Chunks.Add(new DocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = docA.Id,
            ChunkIndex = 0,
            Content = "Contenido confidencial exclusivo de Tenant A.",
            TokenCount = 10,
            VectorId = pointAId.ToString(),
            TenantId = _tenantA
        });

        docB.Chunks.Add(new DocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = docB.Id,
            ChunkIndex = 0,
            Content = "Contenido confidencial exclusivo de Tenant B.",
            TokenCount = 10,
            VectorId = pointBId.ToString(),
            TenantId = _tenantB
        });

        await using var db = CreateDbContext();
        var docRepo = new DocumentRepository(db);
        await docRepo.AddAsync(docA);
        await docRepo.AddAsync(docB);

        var vectorA = CreateDeterministicVector(1.0f);
        var vectorB = CreateDeterministicVector(1.0f); // Mismo vector para forzar que solo el filtro tenantId separe resultados

        var upsertResult = await _vectorStoreService.UpsertPointAsync(
        [
            new VectorPointDto
            {
                PointId = pointAId,
                Vector = vectorA,
                TenantId = _tenantA,
                DocumentId = docA.Id,
                ChunkIndex = 0,
                Content = "Contenido confidencial exclusivo de Tenant A."
            },
            new VectorPointDto
            {
                PointId = pointBId,
                Vector = vectorB,
                TenantId = _tenantB,
                DocumentId = docB.Id,
                ChunkIndex = 0,
                Content = "Contenido confidencial exclusivo de Tenant B."
            }
        ]);

        upsertResult.IsSuccess.Should().BeTrue();
        return (docA, docB);
    }

    [Fact]
    public async Task QdrantSearch_AsTenantA_ShouldReturnOnlyTenantAPoints_NeverTenantB()
    {
        var (docA, docB) = await SeedBothTenantsAsync();

        var searchResult = await _vectorStoreService.SearchAsync(CreateDeterministicVector(1.0f), _tenantA, limit: 10);

        searchResult.IsSuccess.Should().BeTrue();
        searchResult.Value.Should().NotBeNull();
        searchResult.Value!.Should().ContainSingle();
        searchResult.Value![0].DocumentId.Should().Be(docA.Id);
        searchResult.Value!.Should().NotContain(p => p.DocumentId == docB.Id);
    }

    [Fact]
    public async Task QdrantSearch_AsTenantB_ShouldReturnOnlyTenantBPoints_NeverTenantA()
    {
        var (docA, docB) = await SeedBothTenantsAsync();

        var searchResult = await _vectorStoreService.SearchAsync(CreateDeterministicVector(1.0f), _tenantB, limit: 10);

        searchResult.IsSuccess.Should().BeTrue();
        searchResult.Value.Should().NotBeNull();
        searchResult.Value!.Should().ContainSingle();
        searchResult.Value![0].DocumentId.Should().Be(docB.Id);
        searchResult.Value!.Should().NotContain(p => p.DocumentId == docA.Id);
    }

    [Fact]
    public async Task QdrantDelete_AsTenantA_TargetingTenantBDocument_ShouldNotDeleteTenantBVectors()
    {
        var (_, docB) = await SeedBothTenantsAsync();

        // Tenant A intenta eliminar el documento de Tenant B
        var deleteResult = await _vectorStoreService.DeleteByDocumentAsync(docB.Id, _tenantA);
        deleteResult.IsSuccess.Should().BeTrue();

        // Verificación posterior: Tenant B sigue encontrando su vector intacto
        var verifyB = await _vectorStoreService.SearchAsync(CreateDeterministicVector(1.0f), _tenantB, limit: 10);
        verifyB.IsSuccess.Should().BeTrue();
        verifyB.Value!.Should().ContainSingle(p => p.DocumentId == docB.Id);
    }

    [Fact]
    public async Task QdrantDelete_AsTenantA_TargetingOwnDocument_ShouldDeleteOnlyTenantAVectors()
    {
        var (docA, docB) = await SeedBothTenantsAsync();

        var deleteResult = await _vectorStoreService.DeleteByDocumentAsync(docA.Id, _tenantA);
        deleteResult.IsSuccess.Should().BeTrue();

        var verifyA = await _vectorStoreService.SearchAsync(CreateDeterministicVector(1.0f), _tenantA, limit: 10);
        verifyA.IsSuccess.Should().BeTrue();
        verifyA.Value!.Should().BeEmpty();

        var verifyB = await _vectorStoreService.SearchAsync(CreateDeterministicVector(1.0f), _tenantB, limit: 10);
        verifyB.IsSuccess.Should().BeTrue();
        verifyB.Value!.Should().ContainSingle(p => p.DocumentId == docB.Id);
    }

    [Fact]
    public async Task SqlRepositories_ShouldEnforceTenantIsolationForDocumentsAndChunks()
    {
        var (docA, docB) = await SeedBothTenantsAsync();

        await using var db = CreateDbContext();
        var docRepo = new DocumentRepository(db);
        var chunkRepo = new DocumentChunkRepository(db);

        var docsA = (await docRepo.GetByTenantIdAsync(_tenantA)).ToList();
        docsA.Should().ContainSingle(d => d.Id == docA.Id);
        docsA.Should().NotContain(d => d.Id == docB.Id);

        // GetFileNamesByIdsAsync pasando IDs de ambos tenants bajo _tenantA solo devuelve el nombre de Tenant A
        var namesA = await docRepo.GetFileNamesByIdsAsync([docA.Id, docB.Id], _tenantA);
        namesA.Should().ContainKey(docA.Id);
        namesA.Should().NotContainKey(docB.Id);

        // Chunks de docB consultados como Tenant A deben devolver vacío
        var crossChunks = (await chunkRepo.GetByDocumentIdAsync(docB.Id, _tenantA)).ToList();
        crossChunks.Should().BeEmpty();

        // Soft-delete cruzado de chunks de docB como Tenant A no debe eliminar los chunks de Tenant B
        await chunkRepo.DeleteByDocumentIdAsync(docB.Id, _tenantA);
        var chunksB = (await chunkRepo.GetByDocumentIdAsync(docB.Id, _tenantB)).ToList();
        chunksB.Should().ContainSingle();
    }

    [Fact]
    public async Task SqlDocumentRepository_UpdateStatusAsTenantA_OnTenantBDocument_ShouldNotModifyTenantBDocument()
    {
        var (_, docB) = await SeedBothTenantsAsync();

        await using (var db = CreateDbContext())
        {
            var docRepo = new DocumentRepository(db);
            await docRepo.UpdateStatusAsync(docB.Id, _tenantA, DocumentProcessingStatus.Failed, "Intento cruzado");
        }

        await using (var verifyDb = CreateDbContext())
        {
            var docRepoVerify = new DocumentRepository(verifyDb);
            var persistedDocB = await docRepoVerify.GetByIdAsync(docB.Id);
            persistedDocB.Should().NotBeNull();
            persistedDocB!.Status.Should().Be(DocumentProcessingStatus.Completed);
            persistedDocB.ErrorMessage.Should().BeNull();
        }
    }
}
