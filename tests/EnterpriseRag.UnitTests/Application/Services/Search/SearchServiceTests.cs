using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Application.DTOs.Search;
using EnterpriseRag.Core.Application.DTOs.Vector;
using EnterpriseRag.Core.Application.Services.Search;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Search;
using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Interfaces.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Application.Services.Search;

public class SearchServiceTests
{
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IVectorStoreService> _vectorStoreServiceMock;
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly Mock<ILogger<SearchService>> _loggerMock;
    private readonly SearchService _sut;

    public SearchServiceTests()
    {
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _vectorStoreServiceMock = new Mock<IVectorStoreService>();
        _documentRepositoryMock = new Mock<IDocumentRepository>();
        _loggerMock = new Mock<ILogger<SearchService>>();

        _sut = new SearchService(
            _embeddingServiceMock.Object,
            _vectorStoreServiceMock.Object,
            _documentRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SearchAsync_WhenTenantIdIsEmpty_ShouldReturnFailure_WithInvalidTenantIdError()
    {
        var request = new SearchQueryRequestDto { Query = "vacaciones" };

        var result = await _sut.SearchAsync(request, Guid.Empty);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(SearchErrors.InvalidTenantId);
        _embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WhenQueryIsTooShort_ShouldReturnFailure_WithQueryTooShortError()
    {
        var request = new SearchQueryRequestDto { Query = "a" };
        var tenantId = Guid.NewGuid();

        var result = await _sut.SearchAsync(request, tenantId);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(SearchErrors.QueryTooShort(2));
        _embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WhenResultsFound_ShouldFilterByMinScore_AndEnrichWithDocumentName()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var request = new SearchQueryRequestDto
        {
            Query = "politica de seguridad",
            Limit = 5,
            MinScore = 0.70f
        };

        var fakeEmbedding = new ReadOnlyMemory<float>(new float[768]);
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(request.Query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReadOnlyMemory<float>>.Success(fakeEmbedding));

        var qdrantResults = new List<VectorSearchResultDto>
        {
            new()
            {
                PointId = Guid.NewGuid(),
                DocumentId = documentId,
                ChunkIndex = 1,
                Content = "Texto relevante sobre seguridad.",
                Score = 0.85f
            },
            new()
            {
                PointId = Guid.NewGuid(),
                DocumentId = documentId,
                ChunkIndex = 2,
                Content = "Texto con baja relevancia.",
                Score = 0.50f
            }
        };

        _vectorStoreServiceMock
            .Setup(x => x.SearchAsync(fakeEmbedding, tenantId, request.Limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<VectorSearchResultDto>>.Success(qdrantResults));

        var fakeDocument = new Document
        {
            Id = documentId,
            FileName = "Seguridad_2026.pdf",
            ContentType = "application/pdf",
            StoragePath = "docs/seguridad.pdf",
            TenantId = tenantId
        };

        _documentRepositoryMock
            .Setup(x => x.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeDocument);

        var result = await _sut.SearchAsync(request, tenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Score.Should().Be(0.85f);
        result.Value[0].DocumentFileName.Should().Be("Seguridad_2026.pdf");
        result.Value[0].Content.Should().Be("Texto relevante sobre seguridad.");
    }
}
