namespace EnterpriseRag.UnitTests.Security;

using EnterpriseRag.Core.Application.Contracts.Documents;
using EnterpriseRag.Core.Application.DTOs.Documents;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Documents;
using EnterpriseRag.WebApi.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using Xunit;

public class MultiTenantIsolationTests
{
    private readonly Mock<IDocumentService> _documentServiceMock;
    private readonly DocumentsController _sut;

    public MultiTenantIsolationTests()
    {
        _documentServiceMock = new Mock<IDocumentService>();
        _sut = new DocumentsController(_documentServiceMock.Object);
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
    public async Task GetById_WhenUserBelongsToTenantA_AndDocumentBelongsToTenantB_ShouldReturnNotFound()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        SetUserTenantClaim(tenantA);

        // El servicio de dominio detecta que el documento no pertenece a tenantA y devuelve NotFound
        _documentServiceMock
            .Setup(x => x.GetByIdAsync(documentId, tenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DocumentResponseDto>.Failure(DocumentErrors.NotFound));

        // Act
        var result = await _sut.GetById(documentId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var problem = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Document.NotFound");
    }

    [Fact]
    public async Task GetByTenant_ShouldStrictlyUseTenantIdFromClaims_NeverFromUntrustedSources()
    {
        // Arrange
        var userTenantId = Guid.NewGuid();
        SetUserTenantClaim(userTenantId);

        _documentServiceMock
            .Setup(x => x.GetByTenantAsync(userTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<DocumentResponseDto>>.Success([]));

        // Act
        var result = await _sut.GetByTenant();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        _documentServiceMock.Verify(
            x => x.GetByTenantAsync(userTenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void RegisterRequestDto_DoesNotContainTenantIdField_PreventingArbitraryTenantSelection()
    {
        // Assert: Verifica por reflexión arquitectónica que el cliente no tiene capacidad de enviar TenantId en el DTO de registro
        var tenantIdProperty = typeof(EnterpriseRag.Core.Application.DTOs.Account.RegisterRequestDto)
            .GetProperty("TenantId");

        tenantIdProperty.Should().BeNull(
            "por diseño de seguridad B2B estricto, el cliente nunca debe poder suministrar un TenantId arbitrario en el registro");
    }
}
