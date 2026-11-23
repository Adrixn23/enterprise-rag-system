namespace EnterpriseRag.UnitTests.Presentation.Controllers;

using EnterpriseRag.Core.Application.Contracts.Chat;
using EnterpriseRag.Core.Application.DTOs.Chat;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Chat;
using EnterpriseRag.WebApi.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using Xunit;

public class ChatControllerTests
{
    private readonly Mock<IChatService> _chatServiceMock;
    private readonly ChatController _sut;

    public ChatControllerTests()
    {
        _chatServiceMock = new Mock<IChatService>();
        _sut = new ChatController(_chatServiceMock.Object);
    }

    private void SetUserTenantClaim(Guid tenantId)
    {
        var claims = new List<Claim>
        {
            new("tenantId", tenantId.ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task Ask_WhenTenantClaimIsMissing_ShouldReturnUnauthorized()
    {
        // Arrange: Usuario sin claim tenantId
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };

        var request = new ChatRequestDto { Message = "¿Cuál es la política?" };

        // Act
        var result = await _sut.Ask(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
        _chatServiceMock.Verify(
            x => x.AskAsync(It.IsAny<ChatRequestDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Ask_WhenValidRequest_ShouldCallChatServiceWithTenantId_AndReturnOk()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetUserTenantClaim(tenantId);

        var request = new ChatRequestDto { Message = "¿Cuál es el procedimiento?" };
        var expectedResponse = new ChatResponseDto
        {
            Answer = "El procedimiento es...",
            Sources = []
        };

        _chatServiceMock
            .Setup(x => x.AskAsync(request, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ChatResponseDto>.Success(expectedResponse));

        // Act
        var result = await _sut.Ask(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task Ask_WhenServiceReturnsError_ShouldReturnBadRequestWithProblemDetails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetUserTenantClaim(tenantId);

        var request = new ChatRequestDto { Message = "   " };

        _chatServiceMock
            .Setup(x => x.AskAsync(request, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ChatResponseDto>.Failure(ChatErrors.EmptyMessage));

        // Act
        var result = await _sut.Ask(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Chat.EmptyMessage");
    }
}
