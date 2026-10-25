using System.Net.Http.Json;
using System.Text.Json.Serialization;
using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Embeddings;
using EnterpriseRag.Core.Domain.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseRag.Infrastructure.ExternalServices.Embeddings;

public class GeminiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiEmbeddingService> _logger;

    public GeminiEmbeddingService(
        HttpClient httpClient,
        IOptions<GeminiSettings> options,
        ILogger<GeminiEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<Result<ReadOnlyMemory<float>>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        // 1. Validación de entrada (Filtro defensivo)
        if (string.IsNullOrWhiteSpace(text))
            return Result<ReadOnlyMemory<float>>.Failure(EmbeddingErrors.EmptyText);

        try
        {
            // 2. Construcción de la URL de Gemini
            var endpoint = $"v1beta/models/{_settings.ModelId}:embedContent?key={_settings.ApiKey}";

            // 3. Preparación del cuerpo JSON según la especificación de Gemini
            var requestBody = new GeminiEmbedRequest
            {
                Model = $"models/{_settings.ModelId}",
                Content = new GeminiContent
                {
                    Parts = [new GeminiPart { Text = text }]
                }
            };

            // 4. Envío de la petición HTTP POST
            var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, cancellationToken);

            // 5. Manejo de errores HTTP (ej: 400 Bad Request, 403 Invalid API Key, 429 Quota Exceeded)
            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Error en Gemini API ({StatusCode}): {Details}", response.StatusCode, errorDetails);

                return Result<ReadOnlyMemory<float>>.Failure(
                    EmbeddingErrors.ApiError(response.StatusCode.ToString(), errorDetails));
            }

            // 6. Deserialización de la respuesta JSON a nuestro DTO interno
            var result = await response.Content.ReadFromJsonAsync<GeminiEmbedResponse>(cancellationToken: cancellationToken);

            // 7. Validación del contenido recibido (por si Google responde 200 pero sin vector)
            if (result?.Embedding?.Values is null || result.Embedding.Values.Length == 0)
            {
                _logger.LogWarning("Gemini respondió 200 OK pero sin valores de embedding.");
                return Result<ReadOnlyMemory<float>>.Failure(EmbeddingErrors.EmptyResponse);
            }

            // 8. Validación de dimensiones esperadas (text-embedding-004 debe ser 768)
            if (result.Embedding.Values.Length != _settings.EmbeddingDimension)
            {
                _logger.LogError("Dimensión inesperada en el vector. Esperado: {Expected}, Recibido: {Actual}",
                    _settings.EmbeddingDimension, result.Embedding.Values.Length);

                return Result<ReadOnlyMemory<float>>.Failure(
                    EmbeddingErrors.DimensionMismatch(_settings.EmbeddingDimension, result.Embedding.Values.Length));
            }

            // 9. Éxito: conversión implícita de float[] a ReadOnlyMemory<float>
            ReadOnlyMemory<float> vector = result.Embedding.Values;
            return Result<ReadOnlyMemory<float>>.Success(vector);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("La operación para generar embedding fue cancelada.");
            return Result<ReadOnlyMemory<float>>.Failure(EmbeddingErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción inesperada al comunicarse con la API de Gemini.");
            return Result<ReadOnlyMemory<float>>.Failure(EmbeddingErrors.Unexpected(ex.Message));
        }
    }

    public Task<Result<IReadOnlyList<ReadOnlyMemory<float>>>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    // --- Modelos DTO internos para deserializar la respuesta de Google Gemini ---

    private sealed class GeminiEmbedRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public GeminiContent Content { get; set; } = new();
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = [];
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private sealed class GeminiEmbedResponse
    {
        [JsonPropertyName("embedding")]
        public GeminiEmbeddingValues? Embedding { get; set; }
    }

    private sealed class GeminiEmbeddingValues
    {
        [JsonPropertyName("values")]
        public float[]? Values { get; set; }
    }
}
