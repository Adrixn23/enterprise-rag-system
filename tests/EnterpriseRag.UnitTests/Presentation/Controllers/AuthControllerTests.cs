namespace EnterpriseRag.UnitTests.Presentation.Controllers;

using EnterpriseRag.Core.Application.Contracts.Account;
using EnterpriseRag.Core.Application.DTOs.Account;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Account;
using EnterpriseRag.WebApi.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Moq;
using Xunit;

public class AuthControllerTests
{
    private readonly Mock<IAccountService> _accountServiceMock;
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _accountServiceMock = new Mock<IAccountService>();
        _sut = new AuthController(_accountServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
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
            FullName = "Existing User"
        };

        _accountServiceMock
            .Setup(x => x.RegisterAsync(request))
            .ReturnsAsync(Result<string>.Failure(AccountErrors.EmailAlreadyExists));

        // Act
        var actionResult = await _sut.Register(request);

        // Assert
        var conflictResult = actionResult.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var problem = conflictResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Account.EmailAlreadyExists");
    }

    [Fact]
    public void ConfirmEmailPage_ShouldExtractTokenFromUrlHash_ClearFragment_SetSecurityHeaders_AndNotUseInnerHtml()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = _sut.ConfirmEmailPage(userId);

        // Assert: Respuesta HTML segura sin token en servidor
        var contentResult = result.Should().BeOfType<ContentResult>().Subject;
        contentResult.ContentType.Should().Be("text/html");
        contentResult.Content.Should().NotBeNullOrWhiteSpace();
        contentResult.Content.Should().NotContain("innerHTML");
        contentResult.Content.Should().Contain("window.location.hash");
        contentResult.Content.Should().Contain("history.replaceState");
        contentResult.Content.Should().Contain("type='application/json'");
        contentResult.Content.Should().Contain(userId.ToString());

        // Assert: Encabezados de seguridad obligatorios y CSP con nonce sin 'unsafe-inline'
        var headers = _sut.Response.Headers;
        headers.CacheControl.ToString().Should().Contain("no-store");
        headers["Referrer-Policy"].ToString().Should().Be("no-referrer");
        headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");

        var csp = headers.ContentSecurityPolicy.ToString();
        csp.Should().Contain("default-src 'none'");
        csp.Should().Contain("connect-src 'self'");
        csp.Should().Contain("script-src 'nonce-");
        csp.Should().Contain("style-src 'nonce-");
        csp.Should().NotContain("'unsafe-inline'");
    }

    [Fact]
    public void ConfirmEmailPage_WhenCalledTwice_ShouldGenerateUniqueCryptographicNoncePerResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act 1
        _sut.ConfirmEmailPage(userId);
        var csp1 = _sut.Response.Headers.ContentSecurityPolicy.ToString();

        // Act 2 (nuevo HttpContext para segunda respuesta)
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        _sut.ConfirmEmailPage(userId);
        var csp2 = _sut.Response.Headers.ContentSecurityPolicy.ToString();

        // Assert
        csp1.Should().NotBeNullOrWhiteSpace();
        csp2.Should().NotBeNullOrWhiteSpace();
        csp1.Should().NotBe(csp2);
    }

    [Fact]
    public void ConfirmEmailPage_WhenUserIdIsEmpty_ShouldReturnBadRequest()
    {
        // Act
        var result = _sut.ConfirmEmailPage(Guid.Empty);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Account.InvalidConfirmationRequest");
    }

    [Fact]
    public async Task ConfirmEmail_PostWithValidRequest_ShouldConfirmViaAccountService_AndReturnOk()
    {
        // Arrange
        var request = new ConfirmEmailRequestDto
        {
            UserId = Guid.NewGuid(),
            Token = "valid-confirmation-token"
        };

        _accountServiceMock
            .Setup(x => x.ConfirmEmailAsync(request.UserId, request.Token))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.ConfirmEmail(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _accountServiceMock.Verify(x => x.ConfirmEmailAsync(request.UserId, request.Token), Times.Once);
    }

    [Fact]
    public void AuthEndpoints_ShouldHaveRateLimitingAttributesConfigured()
    {
        // Assert: Verificar que Register y ResendConfirmation tienen EnableRateLimitingAttribute
        var registerAttr = typeof(AuthController)
            .GetMethod(nameof(AuthController.Register))!
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), false)
            .Cast<EnableRateLimitingAttribute>()
            .FirstOrDefault();

        var resendAttr = typeof(AuthController)
            .GetMethod(nameof(AuthController.ResendConfirmation))!
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), false)
            .Cast<EnableRateLimitingAttribute>()
            .FirstOrDefault();

        var confirmAttr = typeof(AuthController)
            .GetMethod(nameof(AuthController.ConfirmEmail))!
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), false)
            .Cast<EnableRateLimitingAttribute>()
            .FirstOrDefault();

        registerAttr.Should().NotBeNull();
        registerAttr!.PolicyName.Should().Be("auth-register");

        resendAttr.Should().NotBeNull();
        resendAttr!.PolicyName.Should().Be("auth-resend");

        confirmAttr.Should().NotBeNull();
        confirmAttr!.PolicyName.Should().Be("auth-confirm");
    }
}
