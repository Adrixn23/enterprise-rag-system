using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.FileService;
using EnterpriseRag.Core.Application.Contracts.TextChunker;
using EnterpriseRag.Core.Application.Contracts.TextExtraction;
using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Application.Services.Documents;
using EnterpriseRag.Core.Domain.Common.Errors.Documents;
using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Interfaces.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Security;

public class DocumentServiceIsolationTests
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

    public DocumentServiceIsolationTests()
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
    public async Task GetByIdAsync_WhenDocumentBelongsToTenantB_AndCallerIsTenantA_ShouldReturnNotFound()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        _documentRepositoryMock
            .Setup(x => x.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document
            {
                Id = documentId,
                FileName = "confidencial-tenant-b.pdf",
                ContentType = "application/pdf",
                StoragePath = "tenant-b/confidencial.pdf",
                TenantId = tenantB,
                Status = DocumentProcessingStatus.Completed
            });

        // Act
        var result = await _sut.GetByIdAsync(documentId, tenantA);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(DocumentErrors.NotFound);
    }

    [Fact]
    public async Task GetChunksByDocumentIdAsync_WhenDocumentBelongsToTenantB_AndCallerIsTenantA_ShouldReturnNotFound_AndNeverQueryChunks()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        _documentRepositoryMock
            .Setup(x => x.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document
            {
                Id = documentId,
                FileName = "contrato-b.pdf",
                ContentType = "application/pdf",
                StoragePath = "tenant-b/contrato-b.pdf",
                TenantId = tenantB,
                Status = DocumentProcessingStatus.Completed
            });

        // Act
        var result = await _sut.GetChunksByDocumentIdAsync(documentId, tenantA);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(DocumentErrors.NotFound);
        _documentChunkRepositoryMock.Verify(
            x => x.GetByDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteDocumentAsync_WhenDocumentBelongsToTenantB_AndCallerIsTenantA_ShouldReturnNotFound_AndNeverTouchSqlQdrantOrDisk()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        _documentRepositoryMock
            .Setup(x => x.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document
            {
                Id = documentId,
                FileName = "secreto-b.pdf",
                ContentType = "application/pdf",
                StoragePath = "tenant-b/secreto-b.pdf",
                TenantId = tenantB,
                Status = DocumentProcessingStatus.Completed
            });

        // Act
        var result = await _sut.DeleteDocumentAsync(documentId, tenantA);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(DocumentErrors.NotFound);

        _vectorStoreServiceMock.Verify(
            x => x.DeleteByDocumentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _documentChunkRepositoryMock.Verify(
            x => x.DeleteByDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _fileStorageServiceMock.Verify(
            x => x.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _documentRepositoryMock.Verify(
            x => x.DeleteAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
