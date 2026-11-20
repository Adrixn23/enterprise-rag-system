namespace EnterpriseRag.UnitTests.Presentation.Controllers;

using EnterpriseRag.Core.Application.Contracts.Account;
using EnterpriseRag.Core.Application.DTOs.Account;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Account;
using EnterpriseRag.WebApi.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

public class AuthControllerTests
{
    private readonly Mock<IAccountService> _accountServiceMock;
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _accountServiceMock = new Mock<IAccountService>();
        _sut = new AuthController(_accountServiceMock.Object);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreValid_ShouldReturnOkWithToken()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Email = "admin@ragcorp.com",
            Password = "Password@123!"
        };

        var authResponse = new AuthenticationResponseDto
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            FullName = "Admin User",
            Roles = ["Admin"],
            Token = "header.payload.signature",
            IsAuthenticated = true
        };

        _accountServiceMock
            .Setup(x => x.AuthenticationAsync(request))
            .ReturnsAsync(Result<AuthenticationResponseDto>.Success(authResponse));

        // Act
        var actionResult = await _sut.Login(request);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var responseValue = okResult.Value.Should().BeOfType<AuthenticationResponseDto>().Subject;
        responseValue.Token.Should().Be("header.payload.signature");
        responseValue.Email.Should().Be(request.Email);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreInvalid_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Email = "admin@ragcorp.com",
            Password = "WrongPassword"
        };

        _accountServiceMock
            .Setup(x => x.AuthenticationAsync(request))
            .ReturnsAsync(Result<AuthenticationResponseDto>.Failure(AccountErrors.InvalidCredentials));

        // Act
        var actionResult = await _sut.Login(request);

        // Assert
        var unauthorizedResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = unauthorizedResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Account.InvalidCredentials");
    }

    [Fact]
    public async Task Login_WhenAccountIsLocked_ShouldReturnStatus423Locked()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Email = "locked@ragcorp.com",
            Password = "WrongPassword"
        };

        _accountServiceMock
            .Setup(x => x.AuthenticationAsync(request))
            .ReturnsAsync(Result<AuthenticationResponseDto>.Failure(AccountErrors.AccountLocked));

        // Act
        var actionResult = await _sut.Login(request);

        // Assert
        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status423Locked);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Account.AccountLocked");
    }

    [Fact]
    public async Task Register_WhenRegistrationFails_ShouldReturnBadRequestWithProblemDetails()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            Email = "existing@ragcorp.com",
            Password = "Password@123!",
            ConfirmPassword = "Password@123!",
            FullName = "Existing User",
            TenantId = Guid.NewGuid()
        };

        _accountServiceMock
            .Setup(x => x.RegisterAsync(request))
            .ReturnsAsync(Result<string>.Failure(AccountErrors.EmailAlreadyExists));

        // Act
        var actionResult = await _sut.Register(request);

        // Assert
        var badRequestResult = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var problem = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Account.EmailAlreadyExists");
    }
}
