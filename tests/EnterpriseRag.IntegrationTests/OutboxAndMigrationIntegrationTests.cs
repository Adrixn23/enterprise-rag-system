using System.Security.Claims;
using EnterpriseRag.Core.Application.Contracts.Documents;
using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.FileService;
using EnterpriseRag.Core.Application.Contracts.TextChunker;
using EnterpriseRag.Core.Application.Contracts.TextExtraction;
using EnterpriseRag.Core.Application.Services.Documents;
using EnterpriseRag.Core.Application.Services.TextChunker;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors;
using EnterpriseRag.Core.Domain.Common.Errors.Documents;
using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Entities.Outbox;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Interfaces.Documents;
using EnterpriseRag.Core.Domain.Interfaces.Outbox;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.ExternalServices.FileStorage;
using EnterpriseRag.Infrastructure.ExternalServices.TextExtraction;
using EnterpriseRag.Infrastructure.Persistence.Context;
using EnterpriseRag.Infrastructure.Persistence.Repositories.Documents;
using EnterpriseRag.Infrastructure.Persistence.Repositories.Outbox;
using EnterpriseRag.Infrastructure.VectorStore.VectorStoreService;
using EnterpriseRag.WebApi.Controllers;
using EnterpriseRag.WebApi.Workers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Xunit;

namespace EnterpriseRag.IntegrationTests;

[Collection("SqlAndQdrantIntegration")]
public sealed class OutboxAndMigrationIntegrationTests : IAsyncLifetime
{
    private const string ExpectedDatabaseName = "EnterpriseRagIntegrationTestsDb";
    private readonly string _sqlConnectionString;
    private readonly string _qdrantCollectionName;
    private readonly QdrantClient _qdrantClient;
    private readonly string _tempStorageDir;
    private DbContextOptions<ApplicationDbContext> _dbOptions = null!;

    public OutboxAndMigrationIntegrationTests()
    {
        var rawConnection = Environment.GetEnvironmentVariable("INTEGRATION_SQL_CONNECTION")
            ?? "Server=localhost,1433;Database=EnterpriseRagIntegrationTestsDb;User Id=sa;Password=YourStrong@Pass123!;TrustServerCertificate=True;MultipleActiveResultSets=true;";

        var sqlBuilder = new SqlConnectionStringBuilder(rawConnection);
        if (!string.Equals(sqlBuilder.InitialCatalog, ExpectedDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Solo se permite ejecutar contra '{ExpectedDatabaseName}'.");
        }

        _sqlConnectionString = sqlBuilder.ConnectionString;
        _qdrantCollectionName = $"enterprise-it-outbox-{Guid.NewGuid():N}";
        var qdrantHost = Environment.GetEnvironmentVariable("INTEGRATION_QDRANT_HOST") ?? "localhost";
        var qdrantPort = int.TryParse(Environment.GetEnvironmentVariable("INTEGRATION_QDRANT_PORT"), out var p) ? p : 6334;
        _qdrantClient = new QdrantClient(qdrantHost, qdrantPort);
        _tempStorageDir = Path.Combine(Path.GetTempPath(), $"rag-it-storage-{Guid.NewGuid():N}");
    }

    public async Task InitializeAsync()
    {
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_sqlConnectionString, b => b.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null))
            .Options;

        await using var db = new ApplicationDbContext(_dbOptions);
        await db.Database.EnsureDeletedAsync();
        // Aplica todas las migraciones reales (incluyendo AddOutboxAndDocumentFileHash) sobre SQL Server 2022
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (await _qdrantClient.CollectionExistsAsync(_qdrantCollectionName))
            {
                await _qdrantClient.DeleteCollectionAsync(_qdrantCollectionName);
            }
        }
        finally
        {
            _qdrantClient.Dispose();
            if (Directory.Exists(_tempStorageDir))
            {
                Directory.Delete(_tempStorageDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EndToEnd_Upload202_OutboxClaimLease_WorkerProcess_Pagination_Deduplication_And_DeleteVectors()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var embeddingService = new FakeDeterministicEmbeddingService();

        await using var sp = BuildServiceProvider(embeddingService);

        // 1. Controller Upload -> 202 Accepted + Document(Pending) + OutboxMessage(Pending)
        Guid docId;
        await using (var scope = sp.CreateAsyncScope())
        {
            var docService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
            var controller = CreateController(docService, tenantA);
            var bytes = "Politica corporativa de seguridad multi-tenant."u8.ToArray();
            var formFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "politica.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            var actionResult = await controller.Upload(formFile);
            var accepted = actionResult.Should().BeOfType<AcceptedAtActionResult>().Subject;
            accepted.StatusCode.Should().Be(StatusCodes.Status202Accepted);
            var dto = accepted.Value.Should().BeOfType<Core.Application.DTOs.Documents.DocumentResponseDto>().Subject;
            dto.Status.Should().Be(DocumentProcessingStatus.Pending);
            docId = dto.Id;
        }

        // 2. Deduplicación SHA-256: mismo tenant -> 409 Conflict; otro tenant -> 202 Accepted
        await using (var scope = sp.CreateAsyncScope())
        {
            var docService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
            var dupControllerA = CreateController(docService, tenantA);
            var bytes = "Politica corporativa de seguridad multi-tenant."u8.ToArray();
            var dupFileA = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "politica-copia.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            var dupRes = await dupControllerA.Upload(dupFileA);
            dupRes.Should().BeOfType<ConflictObjectResult>();

            var controllerB = CreateController(docService, tenantB);
            var fileB = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "politica.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };
            (await controllerB.Upload(fileB)).Should().BeOfType<AcceptedAtActionResult>();
        }

        // 3. Concurrencia de lease: dos workers reclaman simultáneamente; ninguno recibe el mismo mensaje dos veces
        await using (var scope1 = sp.CreateAsyncScope())
        await using (var scope2 = sp.CreateAsyncScope())
        {
            var outbox1 = scope1.ServiceProvider.GetRequiredService<IOutboxRepository>();
            var outbox2 = scope2.ServiceProvider.GetRequiredService<IOutboxRepository>();

            var claims = await Task.WhenAll(
                outbox1.ClaimNextBatchAsync(5, TimeSpan.FromMinutes(5)),
                outbox2.ClaimNextBatchAsync(5, TimeSpan.FromMinutes(5)));

            var ids1 = claims[0].Select(m => m.Id).ToList();
            var ids2 = claims[1].Select(m => m.Id).ToList();
            ids1.Intersect(ids2).Should().BeEmpty("el lease atómico debe evitar doble procesamiento");
            (ids1.Count + ids2.Count).Should().Be(2);
        }

        // 4. Recuperación de trabajo abandonado (LockedUntil expirado) + ejecución real de Worker -> SQL Completed + Qdrant vectors
        await using (var db = new ApplicationDbContext(_dbOptions))
        {
            await db.OutboxMessages.ExecuteUpdateAsync(s => s
                .SetProperty(m => m.LockedUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        var worker = new DocumentIngestionWorker(sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<DocumentIngestionWorker>.Instance);
        await worker.ProcessBatchAsync(CancellationToken.None);

        await using (var db = new ApplicationDbContext(_dbOptions))
        {
            var docA = await db.Documents.AsNoTracking().FirstAsync(d => d.Id == docId);
            docA.Status.Should().Be(DocumentProcessingStatus.Completed);

            var chunksA = await db.DocumentChunks.AsNoTracking().Where(c => c.DocumentId == docId).ToListAsync();
            chunksA.Should().NotBeEmpty();

            var outboxAll = await db.OutboxMessages.AsNoTracking().ToListAsync();
            outboxAll.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Processed);
        }

        // 5. Verificar vectores reales en Qdrant y aislamiento de paginación
        await using (var scope = sp.CreateAsyncScope())
        {
            var vectorStore = scope.ServiceProvider.GetRequiredService<Core.Application.Contracts.VectorStore.IVectorStoreService>();
            var queryVec = new ReadOnlyMemory<float>(Enumerable.Repeat(0.25f, 768).ToArray());
            var searchA = await vectorStore.SearchAsync(queryVec, tenantA, 5);
            searchA.IsSuccess.Should().BeTrue();
            searchA.Value!.Should().Contain(r => r.DocumentId == docId);

            var docRepo = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            var (pagedItems, totalCount) = await docRepo.GetPagedByTenantIdAsync(tenantA, page: 1, pageSize: 10);
            totalCount.Should().Be(1);
            pagedItems.Should().ContainSingle(d => d.Id == docId);
        }

        // 6. Aislamiento entre tenants al procesar: tenantB no puede procesar documento de tenantA
        await using (var scope = sp.CreateAsyncScope())
        {
            var docService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
            var crossResult = await docService.ProcessPendingDocumentAsync(docId, tenantB);
            crossResult.IsFailure.Should().BeTrue();
            crossResult.Errors.Should().Contain(DocumentErrors.NotFound);
        }

        // 7. Reintentos, backoff y agotamiento hasta Failed en OutboxRepository real
        await using (var scope = sp.CreateAsyncScope())
        {
            var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            var failingMsg = new OutboxMessage
            {
                TenantId = tenantA,
                AggregateId = Guid.NewGuid(),
                EventType = OutboxEventTypes.DeleteVectors,
                MaxRetries = 2
            };
            await outboxRepo.AddAsync(failingMsg);

            await outboxRepo.ScheduleRetryOrFailAsync(failingMsg.Id, "Error transitorio 1", DateTimeOffset.UtcNow.AddSeconds(10), permanentFailure: false);
            await outboxRepo.ScheduleRetryOrFailAsync(failingMsg.Id, "Error definitivo 2", null, permanentFailure: true);

            await using var db = new ApplicationDbContext(_dbOptions);
            var persisted = await db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == failingMsg.Id);
            persisted.RetryCount.Should().Be(2);
            persisted.Status.Should().Be(OutboxMessageStatus.Failed);
            persisted.LastError.Should().Be("Error definitivo 2");
        }
    }

    private ServiceProvider BuildServiceProvider(IEmbeddingService embeddingService)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:BasePath"] = _tempStorageDir
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddScoped(_ => new ApplicationDbContext(_dbOptions));
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<ITextExtractorService, TextExtractorService>();
        services.AddScoped<ITextChunkerService, TextChunkerService>();
        services.AddSingleton(embeddingService);
        services.AddSingleton(_qdrantClient);
        services.AddSingleton(Options.Create(new QdrantSettings { CollectionName = _qdrantCollectionName, VectorDimension = 768 }));
        services.AddScoped<Core.Application.Contracts.VectorStore.IVectorStoreService, QdrantVectorStoreService>();
        services.AddScoped<IDocumentService, DocumentService>();

        return services.BuildServiceProvider();
    }

    private static DocumentsController CreateController(IDocumentService docService, Guid tenantId)
    {
        var claims = new List<Claim>
        {
            new("tenantId", tenantId.ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        return new DocumentsController(docService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    private sealed class FakeDeterministicEmbeddingService : IEmbeddingService
    {
        public Task<Result<ReadOnlyMemory<float>>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<ReadOnlyMemory<float>>.Success(new ReadOnlyMemory<float>(Enumerable.Repeat(0.25f, 768).ToArray())));

        public Task<Result<IReadOnlyList<ReadOnlyMemory<float>>>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ReadOnlyMemory<float>> list = texts
                .Select(_ => new ReadOnlyMemory<float>(Enumerable.Repeat(0.25f, 768).ToArray()))
                .ToList();
            return Task.FromResult(Result<IReadOnlyList<ReadOnlyMemory<float>>>.Success(list));
        }
    }
}
