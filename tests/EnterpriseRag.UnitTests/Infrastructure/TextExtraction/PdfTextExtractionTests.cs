using System.Text;
using EnterpriseRag.Infrastructure.ExternalServices.TextExtraction;
using FluentAssertions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace EnterpriseRag.UnitTests.Infrastructure.TextExtraction;

public class PdfTextExtractionTests
{
    private readonly TextExtractorService _sut = new();

    [Fact]
    public async Task ExtractTextAsync_WhenPdfHasPositionedWordsWithoutSpaces_ShouldReconstructSpaces()
    {
        // Simula PDFs (como currículums exportados desde Canva/Word/LaTeX) donde cada palabra se posiciona
        // por coordenadas X/Y sin insertar caracteres de espacio explícitos.
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);

        page.AddText("Adrian", 12, new PdfPoint(50, 750), font);
        page.AddText("Francisco", 12, new PdfPoint(95, 750), font);
        page.AddText("Brito", 12, new PdfPoint(160, 750), font);

        var pdfBytes = builder.Build();
        using var stream = new MemoryStream(pdfBytes);

        var extracted = await _sut.ExtractTextAsync(stream, "application/pdf");

        extracted.Trim().Should().Be("Adrian Francisco Brito");
    }

    [Fact]
    public async Task ExtractTextAsync_WhenPdfHasMultipleLinesAndPages_ShouldPreserveReadingOrderAndStructure()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        var page1 = builder.AddPage(PageSize.A4);
        page1.AddText("Segunda", 12, new PdfPoint(100, 700), font);
        page1.AddText("linea", 12, new PdfPoint(160, 700), font);
        page1.AddText("Primera", 12, new PdfPoint(50, 740), font);
        page1.AddText("linea", 12, new PdfPoint(105, 740), font);

        var page2 = builder.AddPage(PageSize.A4);
        page2.AddText("Contenido pagina dos", 12, new PdfPoint(50, 740), font);

        var pdfBytes = builder.Build();
        using var stream = new MemoryStream(pdfBytes);

        var extracted = await _sut.ExtractTextAsync(stream, "application/pdf");
        var lines = extracted
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        lines.Should().ContainInOrder(
            "Primera linea",
            "Segunda linea",
            "Contenido pagina dos");
    }

    [Fact]
    public async Task ExtractTextAsync_WhenPdfContainsCamelCaseEmailsAndUrls_ShouldNotCorruptValidTokens()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);

        page.AddText("EntityFrameworkCore", 11, new PdfPoint(50, 740), font);
        page.AddText("adrian@empresa.com", 11, new PdfPoint(200, 740), font);
        page.AddText("github.com/Adrixn23", 11, new PdfPoint(50, 710), font);

        var pdfBytes = builder.Build();
        using var stream = new MemoryStream(pdfBytes);

        var extracted = await _sut.ExtractTextAsync(stream, "application/pdf");

        extracted.Should().Contain("EntityFrameworkCore");
        extracted.Should().Contain("adrian@empresa.com");
        extracted.Should().Contain("github.com/Adrixn23");
    }

    [Fact]
    public async Task ExtractTextAsync_WhenPdfStreamIsNonSeekable_ShouldExtractTextWithoutError()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("Documento", 12, new PdfPoint(50, 750), font);
        page.AddText("corporativo", 12, new PdfPoint(125, 750), font);

        var pdfBytes = builder.Build();
        using var nonSeekable = new NonSeekableReadStream(pdfBytes);

        var extracted = await _sut.ExtractTextAsync(nonSeekable, "application/pdf");

        extracted.Trim().Should().Be("Documento corporativo");
    }

    [Fact]
    public async Task ExtractTextAsync_WhenPlainText_ShouldPreserveOriginalContent()
    {
        const string expected = "Línea 1: ASP.NET Core\nLínea 2: Qdrant";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(expected));

        var extracted = await _sut.ExtractTextAsync(stream, "text/plain");

        extracted.Should().Be(expected);
    }

    private sealed class NonSeekableReadStream(byte[] buffer) : MemoryStream(buffer)
    {
        public override bool CanSeek => false;
    }
}
