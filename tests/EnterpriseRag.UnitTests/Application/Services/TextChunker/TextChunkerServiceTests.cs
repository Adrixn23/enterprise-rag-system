using EnterpriseRag.Core.Application.Services.TextChunker;
using FluentAssertions;
using Xunit;

namespace EnterpriseRag.UnitTests.Application.Services.TextChunker;

public class TextChunkerServiceTests
{
    private readonly TextChunkerService _sut;

    public TextChunkerServiceTests()
    {
        _sut = new TextChunkerService();
    }

    [Fact]
    public void ChunkText_WhenTextIsNullOrWhitespace_ShouldReturnEmptyList()
    {
        var result = _sut.ChunkText("    ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ChunkText_WhenTextIsShorterThanChunkSize_ShouldReturnSingleChunk()
    {
        var shortText = "Este es un texto corto que cabe perfectamente en un solo chunk de procesamiento.";

        var result = _sut.ChunkText(shortText, chunkSizeInTokens: 100);

        result.Should().HaveCount(1);
        result[0].Should().Be(shortText);
    }

    [Fact]
    public void ChunkText_WhenTextIsLong_ShouldSplitIntoMultipleChunksWithOverlap()
    {
        var paragraph1 = "El sistema Enterprise RAG permite gestionar documentos empresariales de manera segura y eficiente.";
        var paragraph2 = "Cada documento es procesado, dividido en fragmentos y vectorizado en una base de datos Qdrant.";
        var paragraph3 = "Los usuarios pueden realizar consultas en lenguaje natural y obtener respuestas precisas con citas.";
        var fullText = $"{paragraph1}\n\n{paragraph2}\n\n{paragraph3}";

        var result = _sut.ChunkText(fullText, chunkSizeInTokens: 25, overlapTokens: 10);

        result.Should().HaveCountGreaterThan(1);
        result.Should().AllSatisfy(chunk => chunk.Should().NotBeNullOrWhiteSpace());
    }
}
