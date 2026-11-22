using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Application.DTOs.Search;
using EnterpriseRag.Core.Application.DTOs.Vector;
using EnterpriseRag.Core.Application.Services.Search;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Search;
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
    public async Task SearchAsync_WhenResultsFound_ShouldFilterByMinScore_AndPerformSingleBatchQueryForNames()
    {
        var tenantId = Guid.NewGuid();
        var documentId1 = Guid.NewGuid();
        var documentId2 = Guid.NewGuid();

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
                DocumentId = documentId1,
                ChunkIndex = 1,
                Content = "Texto relevante sobre seguridad.",
                Score = 0.85f
            },
            new()
            {
                PointId = Guid.NewGuid(),
                DocumentId = documentId2,
                ChunkIndex = 2,
                Content = "Texto con baja relevancia.",
                Score = 0.50f
            }
        };

        _vectorStoreServiceMock
            .Setup(x => x.SearchAsync(fakeEmbedding, tenantId, request.Limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<VectorSearchResultDto>>.Success(qdrantResults));

        var batchFileNames = new Dictionary<Guid, string>
        {
            { documentId1, "Seguridad_2026.pdf" }
        };

        _documentRepositoryMock
            .Setup(x => x.GetFileNamesByIdsAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(documentId1) && !ids.Contains(documentId2)),
                tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchFileNames);

        var result = await _sut.SearchAsync(request, tenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Score.Should().Be(0.85f);
        result.Value[0].DocumentFileName.Should().Be("Seguridad_2026.pdf");

        // Verificamos que se ejecutó una sola llamada en lote (Cero consultas N+1)
        _documentRepositoryMock.Verify(
            x => x.GetFileNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), tenantId, It.IsAny<CancellationToken>()),
            Times.Once);

        // Verificamos que NUNCA se usó la llamada individual GetByIdAsync
        _documentRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WhenDocumentFileNameIsMissing_ShouldUseNeutralFallbackInsteadOfDesconocido()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var request = new SearchQueryRequestDto
        {
            Query = "politica de seguridad",
            Limit = 5,
            MinScore = 0.60f
        };

        var fakeEmbedding = new ReadOnlyMemory<float>(new float[768]);
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(request.Query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReadOnlyMemory<float>>.Success(fakeEmbedding));

        _vectorStoreServiceMock
            .Setup(x => x.SearchAsync(fakeEmbedding, tenantId, request.Limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<VectorSearchResultDto>>.Success(
            [
                new VectorSearchResultDto
                {
                    PointId = Guid.NewGuid(),
                    DocumentId = documentId,
                    ChunkIndex = 0,
                    Content = "Contenido del documento.",
                    Score = 0.80f
                }
            ]));

        _documentRepositoryMock
            .Setup(x => x.GetFileNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var result = await _sut.SearchAsync(request, tenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].DocumentFileName.Should().Be("Documento");
        result.Value[0].DocumentFileName.Should().NotBe("Desconocido");
    }
}
