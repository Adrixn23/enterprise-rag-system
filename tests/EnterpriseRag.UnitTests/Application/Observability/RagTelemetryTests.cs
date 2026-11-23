using System.Diagnostics.Metrics;
using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.GenerativeAi;
using EnterpriseRag.Core.Application.Contracts.Search;
using EnterpriseRag.Core.Application.DTOs.Chat;
using EnterpriseRag.Core.Application.DTOs.Search;
using EnterpriseRag.Core.Domain.Observability;
using EnterpriseRag.Core.Application.Services.Chat;
using EnterpriseRag.Core.Domain.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Application.Observability;

public class RagTelemetryTests
{
    [Fact]
    public async Task ChatService_AskAsync_ShouldRecordGenerationDurationMetric_WithoutHighCardinalityTags()
    {
        var recordedMetrics = new List<(string Name, double Value, string[] TagKeys)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == RagTelemetry.Meter.Name)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            var keys = tags.ToArray().Select(t => t.Key).ToArray();
            recordedMetrics.Add((instrument.Name, measurement, keys));
        });

        listener.Start();

        var searchMock = new Mock<ISearchService>();
        searchMock
            .Setup(x => x.SearchAsync(It.IsAny<SearchQueryRequestDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<SearchResultDto>>.Success(
            [
                new SearchResultDto
                {
                    PointId = Guid.NewGuid(),
                    DocumentId = Guid.NewGuid(),
                    DocumentFileName = "policy.pdf",
                    ChunkIndex = 0,
                    Content = "Contenido corporativo.",
                    Score = 0.9f
                }
            ]));

        var genMock = new Mock<IGenerativeAiService>();
        genMock
            .Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("Respuesta fundamentada."));

        var sut = new ChatService(searchMock.Object, genMock.Object, new Mock<ILogger<ChatService>>().Object);

        var result = await sut.AskAsync(new ChatRequestDto { Message = "¿Cuál es la política?" }, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        recordedMetrics.Should().Contain(m => m.Name == "rag.generation.duration" && m.Value >= 0);
        foreach (var metric in recordedMetrics)
        {
            metric.TagKeys.Should().NotContain("tenantId");
            metric.TagKeys.Should().NotContain("userId");
            metric.TagKeys.Should().NotContain("prompt");
        }
    }
}
