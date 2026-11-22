using System.Collections.Concurrent;
using System.Net.Mail;
using EnterpriseRag.Core.Application.Contracts.Email;
using EnterpriseRag.Core.Domain.Common.Errors.Account;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.Identity.Entities;
using EnterpriseRag.Infrastructure.Identity.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Identity;

public class IdentityTokenAndResendConcurrencyTests
{
    private sealed class InMemoryUserStore :
        IUserEmailStore<ApplicationUser>,
        IUserSecurityStampStore<ApplicationUser>
    {
        private readonly ConcurrentDictionary<Guid, ApplicationUser> _users = new();

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            if (user.Id == Guid.Empty) user.Id = Guid.NewGuid();
            _users[user.Id] = user;
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            _users[user.Id] = user;
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            _users.TryRemove(user.Id, out _);
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
            => Task.FromResult(Guid.TryParse(userId, out var id) && _users.TryGetValue(id, out var user) ? user : null);

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
            => Task.FromResult(_users.Values.FirstOrDefault(u => u.NormalizedUserName == normalizedUserName));

        public Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(_users.Values.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail));

        public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Email);
        public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken) { user.Email = email; return Task.CompletedTask; }
        public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.EmailConfirmed);
        public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken) { user.EmailConfirmed = confirmed; return Task.CompletedTask; }
        public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedEmail);
        public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken) { user.NormalizedEmail = normalizedEmail; return Task.CompletedTask; }
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
        public Task SetSecurityStampAsync(ApplicationUser user, string stamp, CancellationToken cancellationToken) { user.SecurityStamp = stamp; return Task.CompletedTask; }
        public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.SecurityStamp);
        public void Dispose() { }
    }

    private static (UserManager<ApplicationUser> UserManager, AccountService AccountService, Mock<IEmailSenderService> EmailSenderMock) CreateRealIdentitySut(
        TimeSpan? tokenLifespan = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        services.Configure<DataProtectionTokenProviderOptions>(opt =>
        {
            opt.TokenLifespan = tokenLifespan ?? TimeSpan.FromHours(1);
        });

        services.AddIdentityCore<ApplicationUser>()
            .AddDefaultTokenProviders();

        services.AddSingleton<IUserStore<ApplicationUser>, InMemoryUserStore>();

        var provider = services.BuildServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var jwtOptions = Options.Create(new JWTSettings
        {
            Key = "EnterpriseRag_Super_Secure_Key_256_Bits_Entropy_2026!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            DurationInMinutes = 60
        });

        var emailOptions = Options.Create(new EmailSettings
        {
            PublicBaseUrl = "https://rag.empresa.com",
            Host = "smtp.empresa.com",
            Port = 587,
            FromEmail = "no-reply@empresa.com"
        });

        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var emailSenderMock = new Mock<IEmailSenderService>();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<AccountService>>();

        var signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            userManager,
            provider.GetRequiredService<IHttpContextAccessor>(),
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);

        var accountService = new AccountService(
            userManager,
            signInManagerMock.Object,
            jwtOptions,
            emailOptions,
            envMock.Object,
            emailSenderMock.Object,
            memoryCache,
            loggerMock.Object);

        return (userManager, accountService, emailSenderMock);
    }

    [Fact]
    public async Task RealIdentityToken_ShouldConfirmOnce_AndRejectReuseAndTampering()
    {
        // Arrange
        var (userManager, sut, _) = CreateRealIdentitySut();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "real@empresa.com",
            Email = "real@empresa.com",
            EmailConfirmed = false,
            TenantId = Guid.NewGuid()
        };
        (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();

        var realToken = await userManager.GenerateEmailConfirmationTokenAsync(user);

        // Act 1: Confirmación válida con token real
        var firstConfirm = await sut.ConfirmEmailAsync(user.Id, realToken);

        // Assert 1
        firstConfirm.IsSuccess.Should().BeTrue();
        user.EmailConfirmed.Should().BeTrue();

        // Act 2: Reutilización del mismo token real debe fallar
        var reuseConfirm = await sut.ConfirmEmailAsync(user.Id, realToken);
        reuseConfirm.IsFailure.Should().BeTrue();
        reuseConfirm.Errors.Should().Contain(AccountErrors.InvalidConfirmationToken);

        // Act 3: Token manipulado debe fallar
        var tamperedConfirm = await sut.ConfirmEmailAsync(user.Id, realToken + "tampered");
        tamperedConfirm.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RealIdentityToken_WhenExpired_ShouldBeRejected()
    {
        // Arrange: Token con expiración de 50ms
        var (userManager, sut, _) = CreateRealIdentitySut(TimeSpan.FromMilliseconds(50));
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "expire@empresa.com",
            Email = "expire@empresa.com",
            EmailConfirmed = false,
            TenantId = Guid.NewGuid()
        };
        await userManager.CreateAsync(user);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await Task.Delay(120);

        // Act
        var result = await sut.ConfirmEmailAsync(user.Id, token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(AccountErrors.InvalidConfirmationToken);
        user.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task ResendConfirmationEmail_ConcurrentRequests_ShouldSendOnlyOnce_PreventingRaceCondition()
    {
        // Arrange
        var (userManager, sut, emailSenderMock) = CreateRealIdentitySut();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "race@empresa.com",
            Email = "race@empresa.com",
            EmailConfirmed = false,
            TenantId = Guid.NewGuid()
        };
        await userManager.CreateAsync(user);

        emailSenderMock
            .Setup(x => x.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () => await Task.Delay(20));

        // Act: 10 solicitudes simultáneas para la misma cuenta
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => sut.ResendConfirmationEmailAsync("race@empresa.com"));
        var results = await Task.WhenAll(tasks);

        // Assert: Todas devuelven Success uniforme, pero el envío ocurre exactamente 1 vez
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        emailSenderMock.Verify(
            x => x.SendEmailConfirmationAsync("race@empresa.com", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResendConfirmationEmail_WhenSmtpFails_ShouldNotLockCooldown_AllowingImmediateRetry()
    {
        // Arrange
        var (userManager, sut, emailSenderMock) = CreateRealIdentitySut();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "retry@empresa.com",
            Email = "retry@empresa.com",
            EmailConfirmed = false,
            TenantId = Guid.NewGuid()
        };
        await userManager.CreateAsync(user);

        emailSenderMock
            .SetupSequence(x => x.SendEmailConfirmationAsync("retry@empresa.com", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SmtpException("Transient failure"))
            .Returns(Task.CompletedTask);

        // Act: Primer intento falla SMTP, segundo intento debe poder ejecutarse sin bloqueo por cooldown
        var first = await sut.ResendConfirmationEmailAsync("retry@empresa.com");
        var second = await sut.ResendConfirmationEmailAsync("retry@empresa.com");

        // Assert
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        emailSenderMock.Verify(
            x => x.SendEmailConfirmationAsync("retry@empresa.com", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
