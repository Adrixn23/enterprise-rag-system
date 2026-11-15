using EnterpriseRag.Core.Application.Contracts.GenerativeAi;
using EnterpriseRag.Core.Application.Contracts.Search;
using EnterpriseRag.Core.Application.DTOs.Chat;
using EnterpriseRag.Core.Application.DTOs.Search;
using EnterpriseRag.Core.Application.Services.Chat;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Chat;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Application.Services.Chat;

public class ChatServiceTests
{
    private readonly Mock<ISearchService> _searchServiceMock;
    private readonly Mock<IGenerativeAiService> _generativeAiServiceMock;
    private readonly Mock<ILogger<ChatService>> _loggerMock;
    private readonly ChatService _sut;

    public ChatServiceTests()
    {
        _searchServiceMock = new Mock<ISearchService>();
        _generativeAiServiceMock = new Mock<IGenerativeAiService>();
        _loggerMock = new Mock<ILogger<ChatService>>();

        _sut = new ChatService(
            _searchServiceMock.Object,
            _generativeAiServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task AskAsync_WhenMessageIsEmpty_ShouldReturnFailure_WithEmptyMessageError()
    {
        var request = new ChatRequestDto { Message = "   " };
        var tenantId = Guid.NewGuid();

        var result = await _sut.AskAsync(request, tenantId);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(ChatErrors.EmptyMessage);

        _generativeAiServiceMock.Verify(
            x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AskAsync_WhenValidRequest_ShouldReturnSuccess_WithAnswerAndSources()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var request = new ChatRequestDto
        {
            Message = "¿Cuántos días de vacaciones me corresponden?",
            MaxContextChunks = 3
        };

        var fakeSearchResult = new List<SearchResultDto>
        {
            new()
            {
                PointId = Guid.NewGuid(),
                DocumentId = documentId,
                DocumentFileName = "ReglamentoInterno.pdf",
                ChunkIndex = 0,
                Content = "Los empleados tienen derecho a 14 días de vacaciones.",
                Score = 0.88f
            }
        };

        _searchServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<SearchQueryRequestDto>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<SearchResultDto>>.Success(fakeSearchResult));

        _generativeAiServiceMock
            .Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("Te corresponden 14 días de vacaciones según el ReglamentoInterno.pdf."));

        var result = await _sut.AskAsync(request, tenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Answer.Should().Be("Te corresponden 14 días de vacaciones según el ReglamentoInterno.pdf.");
        result.Value.Sources.Should().HaveCount(1);
        result.Value.Sources[0].DocumentFileName.Should().Be("ReglamentoInterno.pdf");
        result.Value.Sources[0].RelevanceScore.Should().Be(0.88f);

        _generativeAiServiceMock.Verify(
            x => x.GenerateResponseAsync(
                It.Is<string>(system => system.Contains("asistente corporativo")),
                It.Is<string>(user => user.Contains("ReglamentoInterno.pdf") && user.Contains(request.Message)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
