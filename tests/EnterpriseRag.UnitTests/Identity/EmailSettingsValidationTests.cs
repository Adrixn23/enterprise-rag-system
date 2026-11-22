using EnterpriseRag.Core.Application.Contracts.Email;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.Identity.Configuration;
using EnterpriseRag.Infrastructure.Identity.Services;
using EnterpriseRag.IoC;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Identity;

public class EmailSettingsValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://ragcorp.com")]
    [InlineData("/relative/path")]
    public void Validate_WhenPublicBaseUrlIsInvalid_ShouldFailInAllEnvironments(string invalidUrl)
    {
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var validator = new EmailSettingsValidator(envMock.Object);
        var settings = new EmailSettings { PublicBaseUrl = invalidUrl };

        var result = validator.Validate(null, settings);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("PublicBaseUrl");
    }

    [Fact]
    public void Validate_InProduction_WhenPublicBaseUrlIsHttpOrLocalhost_ShouldFail()
    {
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var validator = new EmailSettingsValidator(envMock.Object);
        var settings = new EmailSettings
        {
            PublicBaseUrl = "http://localhost:5235",
            Host = "smtp.ragcorp.com",
            Port = 587,
            FromEmail = "no-reply@ragcorp.com"
        };

        var result = validator.Validate(null, settings);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("HTTPS"));
        result.Failures.Should().Contain(f => f.Contains("localhost/loopback"));
    }

    [Fact]
    public void Validate_InProduction_WhenSmtpFieldsAreMissing_ShouldFail()
    {
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var validator = new EmailSettingsValidator(envMock.Object);
        var settings = new EmailSettings
        {
            PublicBaseUrl = "https://rag.empresa.com",
            Host = "",
            Port = 587,
            FromEmail = "invalid-email"
        };

        var result = validator.Validate(null, settings);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("Host"));
        result.Failures.Should().Contain(f => f.Contains("FromEmail"));
    }

    [Fact]
    public void Validate_InProduction_WhenAllFieldsAreValid_ShouldSucceed()
    {
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var validator = new EmailSettingsValidator(envMock.Object);
        var settings = new EmailSettings
        {
            PublicBaseUrl = "https://rag.empresa.com",
            Host = "smtp.sendgrid.net",
            Port = 587,
            FromEmail = "no-reply@empresa.com"
        };

        var result = validator.Validate(null, settings);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void AddInfrastructureIdentityDependencies_InProduction_RegistersOnlySmtpEmailSender_WithoutBuildServiceProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityConnection"] = "Server=localhost;Database=TestDb;Trusted_Connection=True;",
                ["JWTSettings:Key"] = "EnterpriseRag_Super_Secure_Key_256_Bits_Entropy_2026!",
                ["JWTSettings:Issuer"] = "TestIssuer",
                ["JWTSettings:Audience"] = "TestAudience",
                ["EmailSettings:PublicBaseUrl"] = "https://rag.empresa.com",
                ["EmailSettings:Host"] = "smtp.empresa.com",
                ["EmailSettings:Port"] = "587",
                ["EmailSettings:FromEmail"] = "no-reply@empresa.com"
            })
            .Build();

        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var services = new ServiceCollection();
        services.AddSingleton(envMock.Object);
        services.AddInfrastructureIdentityDependencies(config, envMock.Object);

        var emailDescriptors = services
            .Where(d => d.ServiceType == typeof(IEmailSenderService))
            .ToList();

        emailDescriptors.Should().HaveCount(1);
        emailDescriptors[0].ImplementationType.Should().Be(typeof(SmtpEmailSenderService));
    }

    [Fact]
    public void AddInfrastructureIdentityDependencies_InDevelopment_RegistersOnlyLoggingEmailSender()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityConnection"] = "Server=localhost;Database=TestDb;Trusted_Connection=True;",
                ["JWTSettings:Key"] = "EnterpriseRag_Super_Secure_Key_256_Bits_Entropy_2026!",
                ["JWTSettings:Issuer"] = "TestIssuer",
                ["JWTSettings:Audience"] = "TestAudience"
            })
            .Build();

        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var services = new ServiceCollection();
        services.AddSingleton(envMock.Object);
        services.AddInfrastructureIdentityDependencies(config, envMock.Object);

        var emailDescriptors = services
            .Where(d => d.ServiceType == typeof(IEmailSenderService))
            .ToList();

        emailDescriptors.Should().HaveCount(1);
        emailDescriptors[0].ImplementationType.Should().Be(typeof(LoggingEmailSenderService));
    }
}
