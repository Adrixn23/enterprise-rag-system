using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.GenerativeAi;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Chat;
using EnterpriseRag.IoC;
using EnterpriseRag.WebApi.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EnterpriseRag.UnitTests.Infrastructure.Resilience;

public class HttpResiliencePolicyTests
{
    private const string SecretApiKey = "AIzaSy-RESILIENCE-SECRET-KEY-999";

    [Fact]
    public async Task EmbeddingHttpClient_WhenReceiving429And503_ShouldRetryUpToBoundedLimitAndSucceed()
    {
        var attempts = 0;
        var handler = new SequenceHandler(_ =>
        {
            attempts++;
            if (attempts == 1)
            {
                var tooMany = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                tooMany.Headers.TryAddWithoutValidation("Retry-After", "0");
                return tooMany;
            }

            if (attempts == 2)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    embedding = new { values = new float[768] }
                })
            };
        });

        await using var sp = BuildProvider(handler, useFastRetryDelay: true);
        var embeddingService = sp.GetRequiredService<IEmbeddingService>();
        var generativeService = sp.GetRequiredService<IGenerativeAiService>();
        generativeService.Should().NotBeNull();

        var result = await embeddingService.GenerateEmbeddingAsync("texto de prueba");

        result.IsSuccess.Should().BeTrue();
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task EmbeddingHttpClient_WhenReceivingPersistent500_ShouldStopAfterBoundedRetries()
    {
        var attempts = 0;
        var handler = new SequenceHandler(_ =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server error")
            };
        });

        await using var sp = BuildProvider(handler, useFastRetryDelay: true);
        var embeddingService = sp.GetRequiredService<IEmbeddingService>();

        var result = await embeddingService.GenerateEmbeddingAsync("texto persistente");

        result.IsFailure.Should().BeTrue();
        // 1 intento inicial + MaxRetryAttempts (3) = 4 intentos totales máximo
        attempts.Should().Be(4);
    }

    [Fact]
    public async Task GenerativeHttpClient_When503IsTransient_ShouldRetryAndRecover_AndLogRetryAttempts()
    {
        var attempts = 0;
        var loggerProvider = new RecordingLoggerProvider();
        var handler = new SequenceHandler(_ =>
        {
            attempts++;
            if (attempts < 3)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{\"error\":{\"code\":503,\"status\":\"UNAVAILABLE\"}}")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    candidates = new[]
                    {
                        new { finishReason = "STOP", content = new { parts = new[] { new { text = "Respuesta recuperada" } } } }
                    }
                })
            };
        });

        await using var sp = BuildProvider(handler, useFastRetryDelay: true, loggerProvider);
        var generativeService = sp.GetRequiredService<IGenerativeAiService>();

        var result = await generativeService.GenerateResponseAsync("system-secret-check", "PROMPT_CONFIDENCIAL_USUARIO");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Respuesta recuperada");
        attempts.Should().Be(3);

        loggerProvider.Messages.Should().Contain(m => m.Contains("Reintento HTTP Gemini (generative-ai) — intento 1/3"));
        loggerProvider.Messages.Should().Contain(m => m.Contains("Reintento HTTP Gemini (generative-ai) — intento 2/3"));
        loggerProvider.Messages.Should().NotContain(m => m.Contains(SecretApiKey, StringComparison.Ordinal));
        loggerProvider.Messages.Should().NotContain(m => m.Contains("PROMPT_CONFIDENCIAL_USUARIO", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GenerativeHttpClient_When503Persists_ShouldExhaust4Attempts_ReturnServiceUnavailable503_AndHideInternalJson()
    {
        var attempts = 0;
        var loggerProvider = new RecordingLoggerProvider();
        var handler = new SequenceHandler(_ =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{\"error\":{\"code\":503,\"message\":\"This model is currently experiencing high demand\",\"status\":\"UNAVAILABLE\"}}")
            };
        });

        await using var sp = BuildProvider(handler, useFastRetryDelay: true, loggerProvider);
        var generativeService = sp.GetRequiredService<IGenerativeAiService>();

        var result = await generativeService.GenerateResponseAsync("sys", "PROMPT_SECRETO");

        attempts.Should().Be(4);
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == "Chat.ServiceUnavailable");
        result.Errors.First().Description.Should().NotContain("\"error\"");
        result.Errors.First().Description.Should().NotContain(SecretApiKey);

        var actionResult = result.ToActionResult();
        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Chat.ServiceUnavailable");

        loggerProvider.Messages.Should().NotContain(m => m.Contains(SecretApiKey, StringComparison.Ordinal));
        loggerProvider.Messages.Should().NotContain(m => m.Contains("PROMPT_SECRETO", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GenerativeHttpClient_When429Persists_ShouldExhaust4Attempts_AndMapToHttp429()
    {
        var attempts = 0;
        var handler = new SequenceHandler(_ =>
        {
            attempts++;
            var res = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"error\":{\"code\":429,\"status\":\"RESOURCE_EXHAUSTED\"}}")
            };
            res.Headers.TryAddWithoutValidation("Retry-After", "0");
            return res;
        });

        await using var sp = BuildProvider(handler, useFastRetryDelay: true);
        var generativeService = sp.GetRequiredService<IGenerativeAiService>();

        var result = await generativeService.GenerateResponseAsync("sys", "pregunta");

        attempts.Should().Be(4);
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == "Chat.RateLimited");

        var objectResult = result.ToActionResult().Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task GenerativeHttpClient_WhenRetryAfterHeaderIsProvided_ShouldRespectDelayWithinBounds()
    {
        var attempts = 0;
        var handler = new SequenceHandler(_ =>
        {
            attempts++;
            if (attempts == 1)
            {
                var res = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                res.Headers.TryAddWithoutValidation("Retry-After", "1");
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    candidates = new[]
                    {
                        new { finishReason = "STOP", content = new { parts = new[] { new { text = "ok" } } } }
                    }
                })
            };
        });

        // Delay base de 1ms, pero Retry-After=1s debe imponer ~1s de espera real
        await using var sp = BuildProvider(handler, useFastRetryDelay: true);
        var generativeService = sp.GetRequiredService<IGenerativeAiService>();

        var sw = Stopwatch.StartNew();
        var result = await generativeService.GenerateResponseAsync("sys", "hola");
        sw.Stop();

        result.IsSuccess.Should().BeTrue();
        attempts.Should().Be(2);
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(850);
    }

    private static ServiceProvider BuildProvider(
        HttpMessageHandler handler,
        bool useFastRetryDelay,
        RecordingLoggerProvider? loggerProvider = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GeminiSettings:ApiKey"] = SecretApiKey,
                ["GeminiSettings:ModelId"] = "text-embedding-004",
                ["GeminiSettings:EmbeddingDimension"] = "768",
                ["GeminiSettings:GenerationModelId"] = "gemini-3.5-flash-lite"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            if (loggerProvider is not null)
            {
                builder.AddProvider(loggerProvider);
            }
        });
        services.AddExternalServicesDependencies(config);
        services.ConfigureAll<HttpClientFactoryOptions>(opts =>
        {
            opts.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler);
        });

        if (useFastRetryDelay)
        {
            services.PostConfigureAll<HttpStandardResilienceOptions>(opts =>
            {
                opts.Retry.Delay = TimeSpan.FromMilliseconds(1);
                opts.Retry.UseJitter = false;
            });
        }

        return services.BuildServiceProvider();
    }

    private sealed class SequenceHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responder(request));
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new ListLogger(Messages);
        public void Dispose() { }

        private sealed class ListLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (messages)
                {
                    messages.Add(formatter(state, exception));
                }
            }
        }
    }
}
