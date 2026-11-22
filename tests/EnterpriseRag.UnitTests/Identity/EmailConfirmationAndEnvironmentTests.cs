using EnterpriseRag.Core.Application.Contracts.Email;
using EnterpriseRag.Core.Application.DTOs.Account;
using EnterpriseRag.Core.Domain.Common.Errors.Account;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.Identity.Entities;
using EnterpriseRag.Infrastructure.Identity.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Identity;

public class EmailConfirmationAndEnvironmentTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
    private readonly Mock<IOptions<JWTSettings>> _jwtOptionsMock;
    private readonly Mock<IOptions<EmailSettings>> _emailOptionsMock;
    private readonly Mock<IHostEnvironment> _environmentMock;
    private readonly Mock<IEmailSenderService> _emailSenderMock;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<AccountService>> _loggerMock;

    public EmailConfirmationAndEnvironmentTests()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var contextAccessorMock = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var userClaimsPrincipalFactoryMock = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            _userManagerMock.Object, contextAccessorMock.Object, userClaimsPrincipalFactoryMock.Object, null!, null!, null!, null!);

        _jwtOptionsMock = new Mock<IOptions<JWTSettings>>();
        _jwtOptionsMock.Setup(x => x.Value).Returns(new JWTSettings
        {
            Key = "SuperSecretKeyForIdentityUnitTesting123456789!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            DurationInMinutes = 60
        });

        _emailOptionsMock = new Mock<IOptions<EmailSettings>>();
        _emailOptionsMock.Setup(x => x.Value).Returns(new EmailSettings
        {
            Host = "smtp.ragcorp.com",
            Port = 587,
            FromEmail = "no-reply@ragcorp.com",
            FromName = "Enterprise RAG",
            PublicBaseUrl = "https://app.ragcorp.com"
        });

        _environmentMock = new Mock<IHostEnvironment>();
        _emailSenderMock = new Mock<IEmailSenderService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<AccountService>>();
    }

    private AccountService CreateSut() => new(
        _userManagerMock.Object,
        _signInManagerMock.Object,
        _jwtOptionsMock.Object,
        _emailOptionsMock.Object,
        _environmentMock.Object,
        _emailSenderMock.Object,
        _memoryCache,
        _loggerMock.Object);

    [Fact]
    public async Task Register_InProductionEnvironment_ShouldSetEmailConfirmedFalse_AndSendConfirmationEmail_WithAbsoluteConfiguredUrl()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var request = new RegisterRequestDto
        {
            FullName = "Production User",
            Email = "prod@ragcorp.com",
            Password = "Password@123!",
            ConfirmPassword = "Password@123!"
        };

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        ApplicationUser? capturedUser = null;
        _userManagerMock
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .Callback<ApplicationUser, string>((u, _) => capturedUser = u)
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("valid-secure-token-12345");

        var sut = CreateSut();

        // Act
        var result = await sut.RegisterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedUser.Should().NotBeNull();
        capturedUser!.EmailConfirmed.Should().BeFalse(
            "en entorno de producción la cuenta no debe autoconfirmarse sin validar el buzón de correo");

        _emailSenderMock.Verify(
            x => x.SendEmailConfirmationAsync(
                request.Email,
                It.Is<string>(link =>
                    link.StartsWith($"https://app.ragcorp.com/api/v1/auth/confirm-email?userId={capturedUser.Id}#token=") &&
                    !link.Contains("&token=") &&
                    link.Contains("valid-secure-token-12345")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Register_WhenEmailSendingFails_ShouldNotDeleteUser_AllowingResend()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var request = new RegisterRequestDto
        {
            FullName = "Retry User",
            Email = "retry@ragcorp.com",
            Password = "Password@123!",
            ConfirmPassword = "Password@123!"
        };

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        _userManagerMock
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("token-xyz");

        _emailSenderMock
            .Setup(x => x.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Mail.SmtpException("SMTP connection failed"));

        var sut = CreateSut();

        // Act
        var result = await sut.RegisterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue("el usuario no debe eliminarse si falla el transporte de correo");
        _userManagerMock.Verify(x => x.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Register_InDevelopmentEnvironment_ShouldSetEmailConfirmedTrue_ForLocalDeveloperConvenience()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var request = new RegisterRequestDto
        {
            FullName = "Dev User",
            Email = "dev@ragcorp.com",
            Password = "Password@123!",
            ConfirmPassword = "Password@123!"
        };

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        ApplicationUser? capturedUser = null;
        _userManagerMock
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .Callback<ApplicationUser, string>((u, _) => capturedUser = u)
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.RegisterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedUser.Should().NotBeNull();
        capturedUser!.EmailConfirmed.Should().BeTrue();

        _emailSenderMock.Verify(
            x => x.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Authentication_InProduction_WhenEmailIsNotConfirmed_ShouldReturnFailure_WithEmailNotConfirmed()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var request = new LoginRequestDto
        {
            Email = "unconfirmed@ragcorp.com",
            Password = "Password@123!"
        };

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            TenantId = Guid.NewGuid(),
            EmailConfirmed = false
        };

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        var sut = CreateSut();

        // Act
        var result = await sut.AuthenticationAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(AccountErrors.EmailNotConfirmed);

        _signInManagerMock.Verify(
            x => x.CheckPasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task ConfirmEmail_WithValidToken_ShouldSucceed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "valid-token";
        var user = new ApplicationUser { Id = userId, Email = "user@ragcorp.com", EmailConfirmed = false };

        _userManagerMock
            .Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(x => x.ConfirmEmailAsync(user, token))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.ConfirmEmailAsync(userId, token);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmEmail_WhenTokenAlreadyUsed_ShouldFail()
    {
        // Arrange: Usuario que YA confirmó su cuenta (previene reutilización)
        var userId = Guid.NewGuid();
        var token = "already-used-token";
        var user = new ApplicationUser { Id = userId, Email = "user@ragcorp.com", EmailConfirmed = true };

        _userManagerMock
            .Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        var sut = CreateSut();

        // Act
        var result = await sut.ConfirmEmailAsync(userId, token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(AccountErrors.InvalidConfirmationToken);
        _userManagerMock.Verify(x => x.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResendConfirmationEmail_WhenUserDoesNotExist_ShouldReturnSuccess_PreventingUserEnumeration()
    {
        // Arrange
        _userManagerMock
            .Setup(x => x.FindByEmailAsync("unknown@ragcorp.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.ResendConfirmationEmailAsync("unknown@ragcorp.com");

        // Assert
        result.IsSuccess.Should().BeTrue("evita enumeración de usuarios");
        _emailSenderMock.Verify(
            x => x.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResendConfirmationEmail_CalledRepeatedlyForSameAccount_ShouldEnforceCooldown_AndReturnConsistentSuccess()
    {
        // Arrange
        var email = "cooldown@ragcorp.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            EmailConfirmed = false
        };

        _userManagerMock
            .Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(x => x.GenerateEmailConfirmationTokenAsync(user))
            .ReturnsAsync("token-cooldown-1");

        var sut = CreateSut();

        // Act: Dos solicitudes consecutivas para la misma cuenta
        var firstResult = await sut.ResendConfirmationEmailAsync(email);
        var secondResult = await sut.ResendConfirmationEmailAsync(email);

        // Assert: Ambas devuelven Success consistente (sin revelar estado ni cooldown), pero solo se envía 1 correo
        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsSuccess.Should().BeTrue();

        _emailSenderMock.Verify(
            x => x.SendEmailConfirmationAsync(
                email,
                It.Is<string>(link =>
                    link.StartsWith($"https://app.ragcorp.com/api/v1/auth/confirm-email?userId={user.Id}#token=") &&
                    !link.Contains("&token=") &&
                    link.Contains("token-cooldown-1")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
