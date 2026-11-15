using EnterpriseRag.Core.Application.Contracts.GenerativeAi;
using EnterpriseRag.Core.Application.Contracts.Search;
using EnterpriseRag.Core.Application.DTOs.Chat;
using EnterpriseRag.Core.Application.Services.Chat;
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
}
