using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.GenerativeAi;
using EnterpriseRag.Core.Application.Contracts.Search;
using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Application.DTOs.Chat;
using EnterpriseRag.Core.Application.DTOs.Search;
using EnterpriseRag.Core.Application.DTOs.Vector;
using EnterpriseRag.Core.Application.Services.Chat;
using EnterpriseRag.Core.Application.Services.Search;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Chat;
using EnterpriseRag.Core.Domain.Common.Errors.Search;
using EnterpriseRag.Core.Domain.Interfaces.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Security;

public class SearchAndChatIsolationTests
{
    [Fact]
    public async Task ChatService_WhenTenantIdIsEmpty_ShouldReturnInvalidTenantId_AndNeverCallSearchOrGemini()
    {
        // Arrange
        var searchMock = new Mock<ISearchService>();
        var geminiMock = new Mock<IGenerativeAiService>();
        var sut = new ChatService(searchMock.Object, geminiMock.Object, new Mock<ILogger<ChatService>>().Object);

        // Act
        var result = await sut.AskAsync(new ChatRequestDto { Message = "Hola" }, Guid.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(ChatErrors.InvalidTenantId);
        searchMock.Verify(x => x.SearchAsync(It.IsAny<SearchQueryRequestDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        geminiMock.Verify(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChatService_ShouldPropagateExactTenantId_ToSearchService()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var searchMock = new Mock<ISearchService>();
        var geminiMock = new Mock<IGenerativeAiService>();

        searchMock
            .Setup(x => x.SearchAsync(It.IsAny<SearchQueryRequestDto>(), tenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<SearchResultDto>>.Success([]));

        var sut = new ChatService(searchMock.Object, geminiMock.Object, new Mock<ILogger<ChatService>>().Object);

        // Act
        var result = await sut.AskAsync(new ChatRequestDto { Message = "¿Política?" }, tenantA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        searchMock.Verify(x => x.SearchAsync(It.IsAny<SearchQueryRequestDto>(), tenantA, It.IsAny<CancellationToken>()), Times.Once);
        searchMock.Verify(x => x.SearchAsync(It.IsAny<SearchQueryRequestDto>(), It.Is<Guid>(id => id != tenantA), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchService_WhenTenantIdIsEmpty_ShouldReturnInvalidTenantId_AndNeverCallDownstreamServices()
    {
        // Arrange
        var embeddingMock = new Mock<IEmbeddingService>();
        var vectorMock = new Mock<IVectorStoreService>();
        var docRepoMock = new Mock<IDocumentRepository>();
        var sut = new SearchService(embeddingMock.Object, vectorMock.Object, docRepoMock.Object, new Mock<ILogger<SearchService>>().Object);

        // Act
        var result = await sut.SearchAsync(new SearchQueryRequestDto { Query = "seguridad" }, Guid.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(SearchErrors.InvalidTenantId);
        embeddingMock.Verify(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        vectorMock.Verify(x => x.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        docRepoMock.Verify(x => x.GetFileNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchService_ShouldPropagateExactTenantId_ToVectorStoreAndDocumentRepository()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var embedding = new ReadOnlyMemory<float>(new float[768]);

        var embeddingMock = new Mock<IEmbeddingService>();
        var vectorMock = new Mock<IVectorStoreService>();
        var docRepoMock = new Mock<IDocumentRepository>();

        embeddingMock
            .Setup(x => x.GenerateEmbeddingAsync("seguridad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReadOnlyMemory<float>>.Success(embedding));

        vectorMock
            .Setup(x => x.SearchAsync(embedding, tenantA, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<VectorSearchResultDto>>.Success([
                new VectorSearchResultDto { PointId = Guid.NewGuid(), DocumentId = docId, ChunkIndex = 0, Content = "texto", Score = 0.9f }
            ]));

        docRepoMock
            .Setup(x => x.GetFileNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), tenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { { docId, "doc-a.pdf" } });

        var sut = new SearchService(embeddingMock.Object, vectorMock.Object, docRepoMock.Object, new Mock<ILogger<SearchService>>().Object);

        // Act
        var result = await sut.SearchAsync(new SearchQueryRequestDto { Query = "seguridad", Limit = 5, MinScore = 0.5f }, tenantA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        vectorMock.Verify(x => x.SearchAsync(embedding, tenantA, 5, It.IsAny<CancellationToken>()), Times.Once);
        vectorMock.Verify(x => x.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.Is<Guid>(id => id != tenantA), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

        docRepoMock.Verify(x => x.GetFileNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), tenantA, It.IsAny<CancellationToken>()), Times.Once);
        docRepoMock.Verify(x => x.GetFileNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.Is<Guid>(id => id != tenantA), It.IsAny<CancellationToken>()), Times.Never);
    }
}
