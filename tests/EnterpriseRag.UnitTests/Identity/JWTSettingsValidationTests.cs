using System.ComponentModel.DataAnnotations;
using EnterpriseRag.Core.Domain.Settings;
using FluentAssertions;
using Xunit;

namespace EnterpriseRag.UnitTests.Identity;

public class JWTSettingsValidationTests
{
    [Fact]
    public void Validate_WhenKeyIsEmpty_ShouldFailValidation()
    {
        var settings = new JWTSettings
        {
            Key = "",
            Issuer = "EnterpriseRagApi",
            Audience = "EnterpriseRagClient",
            DurationInMinutes = 60
        };

        var context = new ValidationContext(settings);
        var validationResults = settings.Validate(context).ToList();

        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(v => v.ErrorMessage!.Contains("La clave JWT no puede estar vacía"));
    }

    [Fact]
    public void Validate_WhenKeyHasLessThan32Bytes_ShouldFailValidation()
    {
        var settings = new JWTSettings
        {
            Key = "clave_corta_16_b",
            Issuer = "EnterpriseRagApi",
            Audience = "EnterpriseRagClient",
            DurationInMinutes = 60
        };

        var context = new ValidationContext(settings);
        var validationResults = settings.Validate(context).ToList();

        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(v => v.ErrorMessage!.Contains("La clave JWT es insuficiente"));
    }

    [Fact]
    public void Validate_WhenKeyHasAtLeast32Bytes_ShouldPassValidation()
    {
        var settings = new JWTSettings
        {
            Key = "EnterpriseRag_Super_Secure_Key_256_Bits_Entropy_2026!",
            Issuer = "EnterpriseRagApi",
            Audience = "EnterpriseRagClient",
            DurationInMinutes = 60
        };

        var context = new ValidationContext(settings);
        var validationResults = settings.Validate(context).ToList();

        validationResults.Should().BeEmpty();
    }
}
