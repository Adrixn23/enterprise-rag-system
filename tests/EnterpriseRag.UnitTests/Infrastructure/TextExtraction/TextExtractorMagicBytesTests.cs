using System.Text;
using EnterpriseRag.Infrastructure.ExternalServices.TextExtraction;
using FluentAssertions;
using Xunit;

namespace EnterpriseRag.UnitTests.Infrastructure.TextExtraction;

public class TextExtractorMagicBytesTests
{
    private readonly TextExtractorService _sut;

    public TextExtractorMagicBytesTests()
    {
        _sut = new TextExtractorService();
    }

    [Fact]
    public void IsValidFileContent_WhenPdfHasValidMagicBytes_ShouldReturnTrue()
    {
        // Un PDF real comienza con los bytes %PDF- (0x25, 0x50, 0x44, 0x46, 0x2D)
        var validPdfBytes = "%PDF-1.7\nSample content"u8.ToArray();
        using var stream = new MemoryStream(validPdfBytes);

        var result = _sut.IsValidFileContent(stream, "application/pdf");

        result.Should().BeTrue();
        stream.Position.Should().Be(0); // Debe rebobinar el stream
    }

    [Fact]
    public void IsValidFileContent_WhenExecutableDisguisedAsPdf_ShouldReturnFalse()
    {
        // Un binario ejecutable (.exe / .dll) de Windows comienza con la firma 'MZ' (0x4D, 0x5A)
        var fakePdfBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(fakePdfBytes);

        var result = _sut.IsValidFileContent(stream, "application/pdf");

        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidFileContent_WhenBinaryFileDisguisedAsPlainText_ShouldReturnFalse()
    {
        // Archivo que declara ser text/plain pero contiene bytes nulos binarios (0x00)
        var binaryDisguisedAsText = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x00, 0x6F };
        using var stream = new MemoryStream(binaryDisguisedAsText);

        var result = _sut.IsValidFileContent(stream, "text/plain");

        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidFileContent_WhenPlainTextIsValidUtf8_ShouldReturnTrue()
    {
        var textBytes = Encoding.UTF8.GetBytes("Este es un archivo de texto completamente legítimo sin bytes binarios.");
        using var stream = new MemoryStream(textBytes);

        var result = _sut.IsValidFileContent(stream, "text/plain");

        result.Should().BeTrue();
    }
}
