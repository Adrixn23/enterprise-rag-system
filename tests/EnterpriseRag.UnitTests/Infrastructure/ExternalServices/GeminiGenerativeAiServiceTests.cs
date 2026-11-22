using System.Net;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.ExternalServices.GenerativeAi;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace EnterpriseRag.UnitTests.Infrastructure.ExternalServices;

public class GeminiGenerativeAiServiceTests
{
    private const string SecretApiKey = "AIzaSy-GEN-SECRET-KEY-123456";

    [Fact]
    public async Task GenerateResponseAsync_ShouldSendApiKeyInHeader_NeverInUrlOrLogs()
    {
        var logger = new ListLogger<GeminiGenerativeAiService>();
        string? capturedUri = null;
        string? capturedHeader = null;

        var handler = new StubHandler(req =>
        {
            capturedUri = req.RequestUri?.ToString();
            if (req.Headers.TryGetValues("x-goog-api-key", out var values))
            {
                capturedHeader = string.Join(",", values);
            }

            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent($"Rejected key {SecretApiKey}")
            };
        });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };

        var sut = new GeminiGenerativeAiService(
            client,
            Options.Create(new GeminiSettings { ApiKey = SecretApiKey, GenerationModelId = "gemini-3.5-flash-lite" }),
            logger);

        var result = await sut.GenerateResponseAsync("system", "user");

        result.IsFailure.Should().BeTrue();
        capturedUri.Should().Be("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent");
        capturedUri.Should().NotContain(SecretApiKey);
        capturedUri.Should().NotContain("key=");
        capturedHeader.Should().Be(SecretApiKey);
        logger.Messages.Should().NotContain(m => m.Contains(SecretApiKey, StringComparison.Ordinal));
        result.Errors.Should().NotContain(e => e.Description.Contains(SecretApiKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GenerateResponseAsync_WhenClientCancels_ShouldThrowOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        var handler = new StubHandler(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };

        var sut = new GeminiGenerativeAiService(
            client,
            Options.Create(new GeminiSettings { ApiKey = SecretApiKey }),
            new ListLogger<GeminiGenerativeAiService>());

        Func<Task> act = () => sut.GenerateResponseAsync("system", "user", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
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
