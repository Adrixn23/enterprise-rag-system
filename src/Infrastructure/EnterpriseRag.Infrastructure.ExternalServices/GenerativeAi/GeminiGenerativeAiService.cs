using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using EnterpriseRag.Core.Application.Contracts.GenerativeAi;
using EnterpriseRag.Core.Domain.Observability;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Chat;
using EnterpriseRag.Core.Domain.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseRag.Infrastructure.ExternalServices.GenerativeAi;

public class GeminiGenerativeAiService : IGenerativeAiService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiGenerativeAiService> _logger;

    public GeminiGenerativeAiService(
        HttpClient httpClient,
        IOptions<GeminiSettings> options,
        ILogger<GeminiGenerativeAiService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<Result<string>> GenerateResponseAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return Result<string>.Failure(ChatErrors.EmptyMessage);
        }

        using var activity = RagTelemetry.ActivitySource.StartActivity("gemini.generation");

        try
        {
            var endpoint = $"v1beta/models/{_settings.GenerationModelId}:generateContent";

            var requestBody = new GeminiGenerateRequest
            {
                SystemInstruction = string.IsNullOrWhiteSpace(systemPrompt)
                    ? null
                    : new GeminiContent
                    {
                        Parts = [new GeminiPart { Text = systemPrompt }]
                    },
                Contents =
                [
                    new GeminiContent
                    {
                        Role = "user",
                        Parts = [new GeminiPart { Text = userPrompt }]
                    }
                ],
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = 0.2f, // Temperatura baja para evitar alucinaciones en RAG corporativo
                    MaxOutputTokens = 2048,
                    ThinkingConfig = _settings.GenerationModelId.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase)
                        && !_settings.GenerationModelId.Contains("-lite", StringComparison.OrdinalIgnoreCase)
                        ? new GeminiThinkingConfig { ThinkingLevel = "LOW" }
                        : null
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(requestBody)
            };
            request.Headers.TryAddWithoutValidation("x-goog-api-key", _settings.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = RedactApiKey(await response.Content.ReadAsStringAsync(cancellationToken));
                _logger.LogError("Error en Gemini Generation API ({StatusCode}): {Details}", response.StatusCode, errorDetails);
                RagTelemetry.ExternalErrors.Add(1, new KeyValuePair<string, object?>("dependency", "gemini"), new KeyValuePair<string, object?>("operation", "generate"));

                return response.StatusCode switch
                {
                    HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout =>
                        Result<string>.Failure(ChatErrors.ServiceUnavailable),
                    HttpStatusCode.TooManyRequests =>
                        Result<string>.Failure(ChatErrors.RateLimited),
                    _ => Result<string>.Failure(ChatErrors.GenerationFailed($"El proveedor de IA respondió con estado {(int)response.StatusCode}."))
                };
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>(cancellationToken: cancellationToken);

            // Validar si fue bloqueado por seguridad
            var candidate = result?.Candidates?.FirstOrDefault();
            if (candidate?.FinishReason == "SAFETY")
            {
                _logger.LogWarning("Respuesta de Gemini bloqueada por filtros de seguridad.");
                return Result<string>.Failure(ChatErrors.BlockedBySafety);
            }

            var textResponse = candidate?.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(textResponse))
            {
                _logger.LogWarning("Gemini respondio 200 OK pero sin texto en las partes generadas.");
                return Result<string>.Failure(ChatErrors.EmptyResponse);
            }

            return Result<string>.Success(textResponse);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("La generación de respuesta con Gemini fue cancelada por el cliente.");
            throw; // Propagar limpiamente hacia el pipeline ASP.NET Core
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout al comunicarse con Gemini Generative API.");
            return Result<string>.Failure(ChatErrors.GenerationFailed("Timeout de comunicación con Gemini Generative AI."));
        }
        catch (Exception ex)
        {
            var safeMessage = RedactApiKey(ex.Message);
            _logger.LogError(ex, "Excepcion inesperada al comunicarse con Gemini Generation API.");
            return Result<string>.Failure(ChatErrors.GenerationFailed(safeMessage));
        }
    }

    private string RedactApiKey(string? message)
    {
        if (string.IsNullOrEmpty(message) || string.IsNullOrWhiteSpace(_settings.ApiKey))
            return message ?? string.Empty;

        return message.Replace(_settings.ApiKey, "[REDACTED]", StringComparison.Ordinal);
    }

    // --- Modelos DTO internos para la API REST de Gemini generateContent ---

    private sealed class GeminiGenerateRequest
    {
        [JsonPropertyName("system_instruction")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiContent? SystemInstruction { get; set; }

        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = [];

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig GenerationConfig { get; set; } = new();
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("role")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = [];
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private sealed class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public float Temperature { get; set; } = 0.2f;

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; } = 2048;

        [JsonPropertyName("thinkingConfig")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiThinkingConfig? ThinkingConfig { get; set; }
    }

    private sealed class GeminiThinkingConfig
    {
        [JsonPropertyName("thinkingLevel")]
        public string ThinkingLevel { get; set; } = "LOW";
    }

    private sealed class GeminiGenerateResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
    }
}
