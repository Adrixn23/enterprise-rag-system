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
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Interfaces.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Application.Services.Documents;

public class DocumentPaginationAndDeduplicationTests
{
    private readonly Mock<IDocumentRepository> _docRepoMock = new();
    private readonly Mock<IDocumentChunkRepository> _chunkRepoMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<ITextExtractorService> _extractorMock = new();
    private readonly Mock<ITextChunkerService> _chunkerMock = new();
    private readonly Mock<IEmbeddingService> _embeddingMock = new();
    private readonly Mock<IVectorStoreService> _vectorStoreMock = new();
    private readonly DocumentService _sut;

    public DocumentPaginationAndDeduplicationTests()
    {
        _sut = new DocumentService(
            _docRepoMock.Object,
            _chunkRepoMock.Object,
            _fileStorageMock.Object,
            _extractorMock.Object,
            _chunkerMock.Object,
            _embeddingMock.Object,
            _vectorStoreMock.Object,
            new Mock<ILogger<DocumentService>>().Object);
    }

    [Fact]
    public async Task GetPagedByTenantAsync_ShouldClampPageAndPageSize_AndReturnMetadata()
    {
        var tenantId = Guid.NewGuid();
        var docs = new List<Document>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FileName = "a.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 100,
                StoragePath = "path/a.pdf",
                TenantId = tenantId,
                Status = DocumentProcessingStatus.Completed
            }
        };

        _docRepoMock
            .Setup(x => x.GetPagedByTenantIdAsync(tenantId, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((docs, 250));

        var result = await _sut.GetPagedByTenantAsync(tenantId, page: -3, pageSize: 999);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(100);
        result.Value.TotalCount.Should().Be(250);
        result.Value.TotalPages.Should().Be(3);
        result.Value.HasNextPage.Should().BeTrue();
        result.Value.HasPreviousPage.Should().BeFalse();
        result.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task UploadDocumentAsync_WhenSameHashExistsForSameTenant_ShouldReturnConflict_WithoutCallingStorageOrGemini()
    {
        var tenantId = Guid.NewGuid();
        var content = "%PDF-1.7\nDuplicate content"u8.ToArray();

        _extractorMock.Setup(x => x.CanExtract("application/pdf")).Returns(true);
        _extractorMock.Setup(x => x.IsValidFileContent(It.IsAny<Stream>(), "application/pdf")).Returns(true);
        _docRepoMock
            .Setup(x => x.GetByHashAsync(tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document
            {
                Id = Guid.NewGuid(),
                FileName = "existing.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = content.Length,
                StoragePath = "existing/path",
                TenantId = tenantId
            });

        var request = new UploadDocumentRequestDto
        {
            FileName = "dup.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = content.Length,
            OpenReadStream = () => new MemoryStream(content),
            TenantId = tenantId
        };

        var result = await _sut.UploadDocumentAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(DocumentErrors.DuplicateDocument);
        _fileStorageMock.Verify(x => x.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _embeddingMock.Verify(x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadDocumentAsync_WhenSameHashExistsOnlyForDifferentTenant_ShouldAllowUpload()
    {
        var tenantB = Guid.NewGuid();
        var content = "%PDF-1.7\nShared file content"u8.ToArray();

        _extractorMock.Setup(x => x.CanExtract("application/pdf")).Returns(true);
        _extractorMock.Setup(x => x.IsValidFileContent(It.IsAny<Stream>(), "application/pdf")).Returns(true);
        _docRepoMock
            .Setup(x => x.GetByHashAsync(tenantB, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);
        _fileStorageMock
            .Setup(x => x.SaveFileAsync(It.IsAny<Stream>(), "shared.pdf", tenantB, It.IsAny<CancellationToken>()))
            .ReturnsAsync("path/shared.pdf");
        _extractorMock
            .Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), "application/pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Shared text");
        _chunkerMock
            .Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(["Shared text"]);
        _embeddingMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ReadOnlyMemory<float>>>.Success([new ReadOnlyMemory<float>(new float[768])]));
        _vectorStoreMock
            .Setup(x => x.UpsertPointAsync(It.IsAny<IEnumerable<VectorPointDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var request = new UploadDocumentRequestDto
        {
            FileName = "shared.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = content.Length,
            OpenReadStream = () => new MemoryStream(content),
            TenantId = tenantB
        };

        var result = await _sut.UploadDocumentAsync(request);

        result.IsSuccess.Should().BeTrue();
        _docRepoMock.Verify(x => x.AddAsync(It.Is<Document>(d => d.TenantId == tenantB && !string.IsNullOrEmpty(d.FileHash)), It.IsAny<CancellationToken>()), Times.Once);
    }
}
