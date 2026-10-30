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
        // 1. Validacion de entrada (Filtro defensivo)
        if (string.IsNullOrWhiteSpace(text))
            return Result<ReadOnlyMemory<float>>.Failure(EmbeddingErrors.EmptyText);

        try
        {
            // 2. Construccion de la URL de Gemini
            var endpoint = $"v1beta/models/{_settings.ModelId}:embedContent?key={_settings.ApiKey}";

            // 3. Preparacion del cuerpo JSON segun la especificacion de Gemini
            var requestBody = new GeminiEmbedRequest
            {
                Model = $"models/{_settings.ModelId}",
                Content = new GeminiContent
                {
                    Parts = [new GeminiPart { Text = text }]
                }
            };

            // 4. Envio de la peticion HTTP POST
            var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, cancellationToken);

            // 5. Manejo de errores HTTP (400 Bad Request, 403 Invalid API Key, 429 Quota Exceeded)
            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Error en Gemini API ({StatusCode}): {Details}", response.StatusCode, errorDetails);

                return Result<ReadOnlyMemory<float>>.Failure(
                    EmbeddingErrors.ApiError(response.StatusCode.ToString(), errorDetails));
            }

            // 6. Deserializacion de la respuesta JSON a nuestro DTO interno
            var result = await response.Content.ReadFromJsonAsync<GeminiEmbedResponse>(cancellationToken: cancellationToken);

            // 7. Validacion del contenido recibido (por si Google responde 200 pero sin vector)
            if (result?.Embedding?.Values is null || result.Embedding.Values.Length == 0)
            {
                _logger.LogWarning("Gemini respondio 200 OK pero sin valores de embedding.");
                return Result<ReadOnlyMemory<float>>.Failure(EmbeddingErrors.EmptyResponse);
            }

            // 8. Validacion de dimensiones esperadas (text-embedding-004 debe ser 768)
            if (result.Embedding.Values.Length != _settings.EmbeddingDimension)
            {
                _logger.LogError("Dimension inesperada en el vector. Esperado: {Expected}, Recibido: {Actual}",
                    _settings.EmbeddingDimension, result.Embedding.Values.Length);

                return Result<ReadOnlyMemory<float>>.Failure(
                    EmbeddingErrors.DimensionMismatch(_settings.EmbeddingDimension, result.Embedding.Values.Length));
            }

            // 9. Exito: conversion implicita de float[] a ReadOnlyMemory<float> (zero allocation)
            ReadOnlyMemory<float> vector = result.Embedding.Values;
            return Result<ReadOnlyMemory<float>>.Success(vector);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("La operacion para generar embedding fue cancelada.");
            return Result<ReadOnlyMemory<float>>.Failure(EmbeddingErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepcion inesperada al comunicarse con la API de Gemini.");
            return Result<ReadOnlyMemory<float>>.Failure(EmbeddingErrors.Unexpected(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<ReadOnlyMemory<float>>>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        // 1. Validacion de entrada
        if (texts is null || texts.Count == 0)
            return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(EmbeddingErrors.EmptyList);

        try
        {
            // 2. URL del endpoint batch en Gemini
            var endpoint = $"v1beta/models/{_settings.ModelId}:batchEmbedContents?key={_settings.ApiKey}";

            // 3. Preparacion de la lista de peticiones en un solo paquete JSON
            var batchRequest = new GeminiBatchEmbedRequest
            {
                Requests = texts.Select(t => new GeminiEmbedRequest
                {
                    Model = $"models/{_settings.ModelId}",
                    Content = new GeminiContent
                    {
                        Parts = [new GeminiPart { Text = t }]
                    }
                }).ToList()
            };

            // 4. Envio HTTP en lote (una sola conexion para todos los chunks)
            var response = await _httpClient.PostAsJsonAsync(endpoint, batchRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Error en Gemini Batch Embedding API ({StatusCode}): {Details}", response.StatusCode, errorDetails);

                return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(
                    EmbeddingErrors.ApiError(response.StatusCode.ToString(), errorDetails));
            }

            // 5. Deserializacion de la respuesta
            var result = await response.Content.ReadFromJsonAsync<GeminiBatchEmbedResponse>(cancellationToken: cancellationToken);

            if (result?.Embeddings is null || result.Embeddings.Count == 0)
            {
                _logger.LogWarning("Gemini respondio 200 OK pero la lista de embeddings esta vacia.");
                return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(EmbeddingErrors.EmptyResponse);
            }

            // 6. Conversion de cada float[] a ReadOnlyMemory<float> con validacion de dimensiones
            var vectors = new List<ReadOnlyMemory<float>>(result.Embeddings.Count);
            foreach (var item in result.Embeddings)
            {
                if (item.Values is null || item.Values.Length != _settings.EmbeddingDimension)
                {
                    _logger.LogError("Vector en lote incompleto o con dimension incorrecta.");
                    return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(
                        EmbeddingErrors.DimensionMismatch(_settings.EmbeddingDimension, item.Values?.Length ?? 0));
                }

                vectors.Add(item.Values);
            }

            return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Success(vectors);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("La operacion por lote de embeddings fue cancelada.");
            return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(EmbeddingErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepcion inesperada en batch embeddings con Gemini.");
            return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(EmbeddingErrors.Unexpected(ex.Message));
        }
    }

    // --- Modelos DTO internos para la API REST de Gemini ---

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

    private sealed class GeminiBatchEmbedRequest
    {
        [JsonPropertyName("requests")]
        public List<GeminiEmbedRequest> Requests { get; set; } = [];
    }

    private sealed class GeminiBatchEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<GeminiEmbeddingValues>? Embeddings { get; set; }
    }
}
