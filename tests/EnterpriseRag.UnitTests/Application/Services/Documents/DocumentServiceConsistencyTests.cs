using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.FileService;
using EnterpriseRag.Core.Application.Contracts.TextChunker;
using EnterpriseRag.Core.Application.Contracts.TextExtraction;
using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Application.DTOs.Documents;
using EnterpriseRag.Core.Application.DTOs.Vector;
using EnterpriseRag.Core.Application.Services.Documents;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Documents;
using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Interfaces.Documents;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Application.Services.Documents;

public class DocumentServiceConsistencyTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IDocumentChunkRepository> _documentChunkRepositoryMock;
    private readonly Mock<IFileStorageService> _fileStorageServiceMock;
    private readonly Mock<ITextExtractorService> _textExtractorServiceMock;
    private readonly Mock<ITextChunkerService> _textChunkerServiceMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IVectorStoreService> _vectorStoreServiceMock;
    private readonly Mock<ILogger<DocumentService>> _loggerMock;
    private readonly DocumentService _sut;

    public DocumentServiceConsistencyTests()
    {
        _documentRepositoryMock = new Mock<IDocumentRepository>();
        _documentChunkRepositoryMock = new Mock<IDocumentChunkRepository>();
        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _textExtractorServiceMock = new Mock<ITextExtractorService>();
        _textChunkerServiceMock = new Mock<ITextChunkerService>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _vectorStoreServiceMock = new Mock<IVectorStoreService>();
        _loggerMock = new Mock<ILogger<DocumentService>>();

        _sut = new DocumentService(
            _documentRepositoryMock.Object,
            _documentChunkRepositoryMock.Object,
            _fileStorageServiceMock.Object,
            _textExtractorServiceMock.Object,
            _textChunkerServiceMock.Object,
            _embeddingServiceMock.Object,
            _vectorStoreServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task UploadDocumentAsync_WhenSqlPersistenceThrows_ShouldTriggerCompensationOnQdrantAndFileStorage()
    {
        var tenantId = Guid.NewGuid();
        var storagePath = "docs/sample.pdf";

        // Contenido con firma binaria real (Magic Bytes) de un PDF: %PDF-1.7
        var validPdfBytes = "%PDF-1.7\nSample content"u8.ToArray();
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(validPdfBytes.Length);
        fileMock.Setup(f => f.FileName).Returns("sample.pdf");
        fileMock.Setup(f => f.ContentType).Returns("application/pdf");
        fileMock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(validPdfBytes));

        _textExtractorServiceMock.Setup(x => x.CanExtract("application/pdf")).Returns(true);
        _textExtractorServiceMock
            .Setup(x => x.IsValidFileContent(It.IsAny<Stream>(), "application/pdf"))
            .Returns(true);
        _fileStorageServiceMock
            .Setup(x => x.SaveFileAsync(It.IsAny<Stream>(), "sample.pdf", tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storagePath);

        _textExtractorServiceMock
            .Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), "application/pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Texto extraído del PDF");

        _textChunkerServiceMock
            .Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(["Fragmento 1"]);

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ReadOnlyMemory<float>>>.Success([new ReadOnlyMemory<float>(new float[768])]));

        _vectorStoreServiceMock
            .Setup(x => x.UpsertPointAsync(It.IsAny<IEnumerable<VectorPointDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        
        _documentRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Database connection timed out"));

        var request = new UploadDocumentRequestDto
        {
            File = fileMock.Object,
            TenantId = tenantId
        };

        var result = await _sut.UploadDocumentAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Document.PersistenceFailed");

        // Compensación verificada: Qdrant se limpió y el archivo físico se borró
        _vectorStoreServiceMock.Verify(
            x => x.DeleteByDocumentAsync(It.IsAny<Guid>(), tenantId, It.IsAny<CancellationToken>()),
            Times.Once);

        _fileStorageServiceMock.Verify(
            x => x.DeleteFileAsync(storagePath, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteDocumentAsync_WhenQdrantDeletionFails_ShouldAbortAndNeverDeleteFromSqlOrDisk()
    {
        var documentId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var fakeDocument = new Document
        {
            Id = documentId,
            FileName = "sample.pdf",
            ContentType = "application/pdf",
            StoragePath = "docs/sample.pdf",
            TenantId = tenantId
        };

        _documentRepositoryMock
            .Setup(x => x.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeDocument);

        // Simulamos fallo de red en Qdrant
        _vectorStoreServiceMock
            .Setup(x => x.DeleteByDocumentAsync(documentId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(DocumentErrors.VectorDeletionFailed("Qdrant gRPC unreachable")));

        var result = await _sut.DeleteDocumentAsync(documentId, tenantId);

        result.IsFailure.Should().BeTrue();

        // Verificar que SQL Server y FileStorage quedaron INTACTOS para reintentos
        _documentChunkRepositoryMock.Verify(
            x => x.DeleteByDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _documentRepositoryMock.Verify(
            x => x.DeleteAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _fileStorageServiceMock.Verify(
            x => x.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
