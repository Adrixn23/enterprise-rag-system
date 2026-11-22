using System.Security.Claims;
using EnterpriseRag.Core.Application.Contracts.Chat;
using EnterpriseRag.Core.Application.Contracts.Documents;
using EnterpriseRag.Core.Application.Contracts.Search;
using EnterpriseRag.Core.Application.DTOs.Chat;
using EnterpriseRag.Core.Application.DTOs.Documents;
using EnterpriseRag.Core.Application.DTOs.Search;
using EnterpriseRag.WebApi.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Security;

public class ControllerTenantIsolationTests
{
    public static IEnumerable<object?[]> InvalidTenantClaims()
    {
        yield return [null];
        yield return ["not-a-valid-guid"];
        yield return [Guid.Empty.ToString()];
    }

    private static ControllerContext CreateContextWithTenantClaim(string? tenantClaimValue)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        if (tenantClaimValue != null)
        {
            claims.Add(new Claim("tenantId", tenantClaimValue));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Theory]
    [MemberData(nameof(InvalidTenantClaims))]
    public async Task ChatController_Ask_WithMissingOrInvalidTenantClaim_ShouldReturnUnauthorized_AndNeverCallService(string? tenantClaim)
    {
        var chatServiceMock = new Mock<IChatService>();
        var sut = new ChatController(chatServiceMock.Object)
        {
            ControllerContext = CreateContextWithTenantClaim(tenantClaim)
        };

        var result = await sut.Ask(new ChatRequestDto { Message = "¿Políticas?" });

        result.Should().BeOfType<UnauthorizedResult>();
        chatServiceMock.Verify(x => x.AskAsync(It.IsAny<ChatRequestDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(InvalidTenantClaims))]
    public async Task SearchController_Search_WithMissingOrInvalidTenantClaim_ShouldReturnUnauthorized_AndNeverCallService(string? tenantClaim)
    {
        var searchServiceMock = new Mock<ISearchService>();
        var sut = new SearchController(searchServiceMock.Object)
        {
            ControllerContext = CreateContextWithTenantClaim(tenantClaim)
        };

        var result = await sut.Search(new SearchQueryRequestDto { Query = "políticas" });

        result.Should().BeOfType<UnauthorizedResult>();
        searchServiceMock.Verify(x => x.SearchAsync(It.IsAny<SearchQueryRequestDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(InvalidTenantClaims))]
    public async Task DocumentsController_AllEndpoints_WithMissingOrInvalidTenantClaim_ShouldReturnUnauthorized_AndNeverCallService(string? tenantClaim)
    {
        var documentServiceMock = new Mock<IDocumentService>();
        var sut = new DocumentsController(documentServiceMock.Object)
        {
            ControllerContext = CreateContextWithTenantClaim(tenantClaim)
        };

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(10);

        (await sut.Upload(fileMock.Object)).Should().BeOfType<UnauthorizedResult>();
        (await sut.GetById(Guid.NewGuid())).Should().BeOfType<UnauthorizedResult>();
        (await sut.GetByTenant()).Should().BeOfType<UnauthorizedResult>();
        (await sut.GetChunks(Guid.NewGuid())).Should().BeOfType<UnauthorizedResult>();
        (await sut.Delete(Guid.NewGuid())).Should().BeOfType<UnauthorizedResult>();

        documentServiceMock.Verify(x => x.UploadDocumentAsync(It.IsAny<UploadDocumentRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        documentServiceMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        documentServiceMock.Verify(x => x.GetByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        documentServiceMock.Verify(x => x.GetChunksByDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        documentServiceMock.Verify(x => x.DeleteDocumentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
