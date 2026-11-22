using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EnterpriseRag.Core.Domain.Common.Errors.Embeddings;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.ExternalServices.Embeddings;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace EnterpriseRag.UnitTests.Infrastructure.ExternalServices;

public class GeminiEmbeddingServiceTests
{
    private const string SecretApiKey = "AIzaSy-TEST-SECRET-KEY-987654321";

    private static GeminiSettings CreateSettings(int dimension = 4) => new()
    {
        ApiKey = SecretApiKey,
        ModelId = "text-embedding-004",
        EmbeddingDimension = dimension,
        GenerationModelId = "gemini-1.5-flash"
    };

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenListIsEmpty_ShouldReturnEmptyListFailure_WithoutHttpCall()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("No debe llamarse HTTP"));
        var sut = CreateSut(handler);

        var result = await sut.GenerateEmbeddingsAsync([]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(EmbeddingErrors.EmptyList);
        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(100, 1)]
    [InlineData(105, 2)]
    [InlineData(250, 3)]
    public async Task GenerateEmbeddingsAsync_ShouldSplitIntoMax100Batches_AndPreserveExactOrder(
        int totalItems,
        int expectedBatches)
    {
        var handler = new RecordingHandler(req =>
        {
            var body = JsonDocument.Parse(req.Body).RootElement;
            var requests = body.GetProperty("requests");
            var embeddings = new List<object>();

            foreach (var item in requests.EnumerateArray())
            {
                var text = item.GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()!;
                var index = int.Parse(text.Replace("chunk-", string.Empty));
                embeddings.Add(new { values = new float[] { index, index + 0.1f, index + 0.2f, index + 0.3f } });
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { embeddings })
            };
        });

        var sut = CreateSut(handler, dimension: 4);
        var inputs = Enumerable.Range(0, totalItems).Select(i => $"chunk-{i}").ToList();

        var result = await sut.GenerateEmbeddingsAsync(inputs);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(totalItems);
        handler.Requests.Should().HaveCount(expectedBatches);

        for (int i = 0; i < totalItems; i++)
        {
            result.Value![i].Span[0].Should().Be(i);
        }
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenSecondBatchFails_ShouldReturnFailure_WithoutPartialResults()
    {
        int callCount = 0;
        var handler = new RecordingHandler(req =>
        {
            callCount++;
            if (callCount == 1)
            {
                var body = JsonDocument.Parse(req.Body).RootElement;
                var count = body.GetProperty("requests").GetArrayLength();
                var embeddings = Enumerable.Range(0, count)
                    .Select(i => new { values = new float[] { i, 0, 0, 0 } })
                    .ToList();

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { embeddings })
                };
            }

            return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"error\":\"rate limit\"}")
            };
        });

        var sut = CreateSut(handler, dimension: 4);
        var inputs = Enumerable.Range(0, 120).Select(i => $"chunk-{i}").ToList();

        var result = await sut.GenerateEmbeddingsAsync(inputs);

        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateEmbeddingAndBatch_ShouldSendApiKeyInHeader_NeverInUrlOrLogs()
    {
        var logger = new ListLogger<GeminiEmbeddingService>();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent($"Invalid request using {SecretApiKey}")
        });

        var sut = CreateSut(handler, dimension: 4, logger: logger);

        var singleResult = await sut.GenerateEmbeddingAsync("hola");
        var batchResult = await sut.GenerateEmbeddingsAsync(["hola"]);

        singleResult.IsFailure.Should().BeTrue();
        batchResult.IsFailure.Should().BeTrue();

        handler.Requests.Should().HaveCount(2);
        foreach (var req in handler.Requests)
        {
            req.Uri.Should().NotContain(SecretApiKey);
            req.Uri.Should().NotContain("key=");
            req.Headers.TryGetValue("x-goog-api-key", out var headerVal).Should().BeTrue();
            headerVal.Should().Be(SecretApiKey);
        }

        logger.Messages.Should().NotContain(m => m.Contains(SecretApiKey, StringComparison.Ordinal));
        singleResult.Errors.Should().NotContain(e => e.Description.Contains(SecretApiKey, StringComparison.Ordinal));
        batchResult.Errors.Should().NotContain(e => e.Description.Contains(SecretApiKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenClientCancels_ShouldThrowOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        var handler = new RecordingHandler(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        var sut = CreateSut(handler, dimension: 4);

        Func<Task> act = () => sut.GenerateEmbeddingsAsync(["a", "b"], cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenHttpClientTimesOut_ShouldReturnTimeoutFailure_NotThrow()
    {
        var handler = new RecordingHandler(_ => throw new TaskCanceledException("HttpClient.Timeout elapsed"));
        var sut = CreateSut(handler, dimension: 4);

        var result = await sut.GenerateEmbeddingsAsync(["a", "b"], CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Embedding.ApiError" && e.Description.Contains("504"));
    }

    private static GeminiEmbeddingService CreateSut(
        RecordingHandler handler,
        int dimension = 4,
        ILogger<GeminiEmbeddingService>? logger = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };

        return new GeminiEmbeddingService(
            httpClient,
            Options.Create(CreateSettings(dimension)),
            logger ?? new ListLogger<GeminiEmbeddingService>());
    }

    private sealed record RecordedRequest(string Uri, string Body, Dictionary<string, string> Headers);

    private sealed class RecordingHandler(Func<RecordedRequest, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var body = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : string.Empty;

            var headers = request.Headers.ToDictionary(
                h => h.Key,
                h => string.Join(",", h.Value),
                StringComparer.OrdinalIgnoreCase);

            var recorded = new RecordedRequest(request.RequestUri?.ToString() ?? string.Empty, body, headers);
            Requests.Add(recorded);

            return responder(recorded);
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
