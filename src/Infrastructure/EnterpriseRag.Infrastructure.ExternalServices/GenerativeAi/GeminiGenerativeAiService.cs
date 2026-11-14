using System.Net.Http.Json;
using System.Text.Json.Serialization;
using EnterpriseRag.Core.Application.Contracts.GenerativeAi;
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

        try
        {
            var endpoint = $"v1beta/models/{_settings.GenerationModelId}:generateContent?key={_settings.ApiKey}";

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
                    MaxOutputTokens = 2048
                }
            };

            var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Error en Gemini Generation API ({StatusCode}): {Details}", response.StatusCode, errorDetails);
                return Result<string>.Failure(ChatErrors.GenerationFailed($"Status {response.StatusCode}: {errorDetails}"));
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
        catch (OperationCanceledException)
        {
            return Result<string>.Failure(ChatErrors.GenerationFailed("La operacion fue cancelada."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepcion inesperada al comunicarse con Gemini Generation API.");
            return Result<string>.Failure(ChatErrors.GenerationFailed(ex.Message));
        }
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
