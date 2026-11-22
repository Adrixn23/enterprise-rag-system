using EnterpriseRag.Core.Application.Contracts.Documents;
using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.FileService;
using EnterpriseRag.Core.Application.Contracts.TextChunker;
using EnterpriseRag.Core.Application.Contracts.TextExtraction;
using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Application.DTOs.Documents;
using EnterpriseRag.Core.Application.DTOs.Vector;
using EnterpriseRag.Core.Application.Services.Documents;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Entities.Outbox;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Interfaces.Documents;
using EnterpriseRag.Core.Domain.Interfaces.Outbox;
using EnterpriseRag.WebApi.Workers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Application.Services.Documents;

public class OutboxAndAsyncIngestionTests
{
    [Fact]
    public async Task EnqueueDocumentAsync_ShouldPersistPendingDocumentAndOutboxMessage_WithoutCallingGeminiOrQdrant()
    {
        var docRepoMock = new Mock<IDocumentRepository>();
        var chunkRepoMock = new Mock<IDocumentChunkRepository>();
        var fileStorageMock = new Mock<IFileStorageService>();
        var extractorMock = new Mock<ITextExtractorService>();
        var chunkerMock = new Mock<ITextChunkerService>();
        var embeddingMock = new Mock<IEmbeddingService>();
        var vectorStoreMock = new Mock<IVectorStoreService>();
        var outboxMock = new Mock<IOutboxRepository>();

        var tenantId = Guid.NewGuid();
        var content = "%PDF-1.7\nAsync ingestion test"u8.ToArray();

        extractorMock.Setup(x => x.CanExtract("application/pdf")).Returns(true);
        extractorMock.Setup(x => x.IsValidFileContent(It.IsAny<Stream>(), "application/pdf")).Returns(true);
        fileStorageMock
            .Setup(x => x.SaveFileAsync(It.IsAny<Stream>(), "async.pdf", tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("path/async.pdf");

        var sut = new DocumentService(
            docRepoMock.Object,
            chunkRepoMock.Object,
            fileStorageMock.Object,
            extractorMock.Object,
            chunkerMock.Object,
            embeddingMock.Object,
            vectorStoreMock.Object,
            new Mock<ILogger<DocumentService>>().Object,
            outboxMock.Object);

        var result = await sut.EnqueueDocumentAsync(new UploadDocumentRequestDto
        {
            FileName = "async.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = content.Length,
            OpenReadStream = () => new MemoryStream(content),
            TenantId = tenantId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(DocumentProcessingStatus.Pending);

        docRepoMock.Verify(x => x.AddWithOutboxAsync(
            It.Is<Document>(d => d.TenantId == tenantId && d.Status == DocumentProcessingStatus.Pending),
            It.Is<OutboxMessage>(m => m.TenantId == tenantId && m.EventType == OutboxEventTypes.IngestDocument && m.Status == OutboxMessageStatus.Pending),
            It.IsAny<CancellationToken>()), Times.Once);

        embeddingMock.Verify(x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        vectorStoreMock.Verify(x => x.UpsertPointAsync(It.IsAny<IEnumerable<VectorPointDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPendingDocumentAsync_WhenAlreadyCompleted_ShouldReturnSuccessIdempotently()
    {
        var docRepoMock = new Mock<IDocumentRepository>();
        var chunkRepoMock = new Mock<IDocumentChunkRepository>();
        var fileStorageMock = new Mock<IFileStorageService>();
        var extractorMock = new Mock<ITextExtractorService>();
        var chunkerMock = new Mock<ITextChunkerService>();
        var embeddingMock = new Mock<IEmbeddingService>();
        var vectorStoreMock = new Mock<IVectorStoreService>();

        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        docRepoMock
            .Setup(x => x.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document
            {
                Id = docId,
                FileName = "done.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 100,
                StoragePath = "path/done.pdf",
                TenantId = tenantId,
                Status = DocumentProcessingStatus.Completed
            });

        var sut = new DocumentService(
            docRepoMock.Object,
            chunkRepoMock.Object,
            fileStorageMock.Object,
            extractorMock.Object,
            chunkerMock.Object,
            embeddingMock.Object,
            vectorStoreMock.Object,
            new Mock<ILogger<DocumentService>>().Object);

        var result = await sut.ProcessPendingDocumentAsync(docId, tenantId);

        result.IsSuccess.Should().BeTrue();
        embeddingMock.Verify(x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        vectorStoreMock.Verify(x => x.UpsertPointAsync(It.IsAny<IEnumerable<VectorPointDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DocumentIngestionWorker_WhenProcessingFails_ShouldScheduleBackoffRetry()
    {
        var outboxMock = new Mock<IOutboxRepository>();
        var docServiceMock = new Mock<IDocumentService>();
        var vectorStoreMock = new Mock<IVectorStoreService>();

        var msg = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AggregateId = Guid.NewGuid(),
            EventType = OutboxEventTypes.IngestDocument,
            RetryCount = 0,
            MaxRetries = 5
        };

        outboxMock
            .Setup(x => x.ClaimNextBatchAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([msg]);

        docServiceMock
            .Setup(x => x.ProcessPendingDocumentAsync(msg.AggregateId, msg.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(new EnterpriseRag.Core.Domain.Common.Errors.Error("Qdrant.Unavailable", "Qdrant caído")));

        var services = new ServiceCollection();
        services.AddScoped(_ => outboxMock.Object);
        services.AddScoped(_ => docServiceMock.Object);
        services.AddScoped(_ => vectorStoreMock.Object);
        await using var sp = services.BuildServiceProvider();

        var worker = new DocumentIngestionWorker(
            sp.GetRequiredService<IServiceScopeFactory>(),
            new Mock<ILogger<DocumentIngestionWorker>>().Object);

        await worker.ProcessBatchAsync(CancellationToken.None);

        outboxMock.Verify(x => x.ScheduleRetryOrFailAsync(
            msg.Id,
            "Qdrant caído",
            It.Is<DateTimeOffset?>(dt => dt.HasValue && dt.Value > DateTimeOffset.UtcNow),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
