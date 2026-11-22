using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Domain.Observability;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Embeddings;
using EnterpriseRag.Core.Domain.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseRag.Infrastructure.ExternalServices.Embeddings;

public class GeminiEmbeddingService : IEmbeddingService
{
    public const int MaxBatchSize = 100;

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

        using var activity = RagTelemetry.ActivitySource.StartActivity("gemini.embedding.single");
        var sw = Stopwatch.StartNew();

        try
        {
            // 2. Construccion de la URL de Gemini sin credenciales en query string
            var endpoint = $"v1beta/models/{_settings.ModelId}:embedContent";

            // 3. Preparacion del cuerpo JSON segun la especificacion de Gemini
            var requestBody = new GeminiEmbedRequest
            {
                Model = $"models/{_settings.ModelId}",
                Content = new GeminiContent
                {
                    Parts = [new GeminiPart { Text = text }]
                },
                OutputDimensionality = _settings.EmbeddingDimension
            };

            // 4. Envio de la peticion HTTP POST con API Key en header
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(requestBody)
            };
            request.Headers.TryAddWithoutValidation("x-goog-api-key", _settings.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            // 5. Manejo de errores HTTP (400 Bad Request, 403 Invalid API Key, 429 Quota Exceeded)
            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = RedactApiKey(await response.Content.ReadAsStringAsync(cancellationToken));
                _logger.LogError("Error en Gemini API ({StatusCode}): {Details}", response.StatusCode, errorDetails);
                RagTelemetry.ExternalErrors.Add(1, new KeyValuePair<string, object?>("dependency", "gemini"), new KeyValuePair<string, object?>("operation", "embed_single"));

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("La generación de embeddings fue cancelada por solicitud del cliente.");
            throw; // Propagar limpiamente hacia el pipeline ASP.NET Core
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout al comunicarse con Gemini Embeddings API.");
            RagTelemetry.ExternalErrors.Add(1, new KeyValuePair<string, object?>("dependency", "gemini"), new KeyValuePair<string, object?>("operation", "embed_single"));
            return Result<ReadOnlyMemory<float>>.Failure(EmbeddingErrors.ApiError("504", "Timeout de servicio externo"));
        }
        catch (Exception ex)
        {
            var safeMessage = RedactApiKey(ex.Message);
            _logger.LogError(ex, "Excepcion inesperada al comunicarse con la API de Gemini.");
            RagTelemetry.ExternalErrors.Add(1, new KeyValuePair<string, object?>("dependency", "gemini"), new KeyValuePair<string, object?>("operation", "embed_single"));
            return Result<ReadOnlyMemory<float>>.Failure(EmbeddingErrors.Unexpected(safeMessage));
        }
        finally
        {
            RagTelemetry.EmbeddingsDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("operation", "single"));
        }
    }

    public async Task<Result<IReadOnlyList<ReadOnlyMemory<float>>>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        // 1. Validacion de entrada
        if (texts is null || texts.Count == 0)
            return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(EmbeddingErrors.EmptyList);

        using var activity = RagTelemetry.ActivitySource.StartActivity("gemini.embedding.batch");
        var sw = Stopwatch.StartNew();

        try
        {
            // 2. URL del endpoint batch en Gemini sin credenciales en query string
            var endpoint = $"v1beta/models/{_settings.ModelId}:batchEmbedContents";
            var allVectors = new List<ReadOnlyMemory<float>>(texts.Count);

            // 3. Particion en lotes <= MaxBatchSize (100, limite oficial de Gemini batchEmbedContents)
            foreach (var batch in texts.Chunk(MaxBatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batchRequest = new GeminiBatchEmbedRequest
                {
                    Requests = batch.Select(t => new GeminiEmbedRequest
                    {
                        Model = $"models/{_settings.ModelId}",
                        Content = new GeminiContent
                        {
                            Parts = [new GeminiPart { Text = t }]
                        },
                        OutputDimensionality = _settings.EmbeddingDimension
                    }).ToList()
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = JsonContent.Create(batchRequest)
                };
                request.Headers.TryAddWithoutValidation("x-goog-api-key", _settings.ApiKey);

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = RedactApiKey(await response.Content.ReadAsStringAsync(cancellationToken));
                    _logger.LogError("Error en Gemini Batch Embedding API ({StatusCode}): {Details}", response.StatusCode, errorDetails);

                    return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(
                        EmbeddingErrors.ApiError(response.StatusCode.ToString(), errorDetails));
                }

                var result = await response.Content.ReadFromJsonAsync<GeminiBatchEmbedResponse>(cancellationToken: cancellationToken);

                if (result?.Embeddings is null || result.Embeddings.Count == 0)
                {
                    _logger.LogWarning("Gemini respondio 200 OK pero la lista de embeddings esta vacia.");
                    return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(EmbeddingErrors.EmptyResponse);
                }

                if (result.Embeddings.Count != batch.Length)
                {
                    _logger.LogError("Cantidad de embeddings devuelta ({Actual}) no coincide con el lote enviado ({Expected}).",
                        result.Embeddings.Count, batch.Length);
                    return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(
                        EmbeddingErrors.ApiError("CountMismatch", $"Se esperaban {batch.Length} embeddings y se recibieron {result.Embeddings.Count}."));
                }

                foreach (var item in result.Embeddings)
                {
                    if (item.Values is null || item.Values.Length != _settings.EmbeddingDimension)
                    {
                        _logger.LogError("Vector en lote incompleto o con dimension incorrecta.");
                        return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(
                            EmbeddingErrors.DimensionMismatch(_settings.EmbeddingDimension, item.Values?.Length ?? 0));
                    }

                    allVectors.Add(item.Values);
                }
            }

            return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Success(allVectors);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("La operacion por lote de embeddings fue cancelada por solicitud del cliente.");
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout al comunicarse con Gemini Batch Embeddings API.");
            RagTelemetry.ExternalErrors.Add(1, new KeyValuePair<string, object?>("dependency", "gemini"), new KeyValuePair<string, object?>("operation", "embed_batch"));
            return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(
                EmbeddingErrors.ApiError("504", "Timeout de servicio externo en batch embeddings."));
        }
        catch (Exception ex)
        {
            var safeMessage = RedactApiKey(ex.Message);
            _logger.LogError(ex, "Excepcion inesperada en batch embeddings con Gemini.");
            RagTelemetry.ExternalErrors.Add(1, new KeyValuePair<string, object?>("dependency", "gemini"), new KeyValuePair<string, object?>("operation", "embed_batch"));
            return Result<IReadOnlyList<ReadOnlyMemory<float>>>.Failure(EmbeddingErrors.Unexpected(safeMessage));
        }
        finally
        {
            RagTelemetry.EmbeddingsDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("operation", "batch"));
        }
    }

    private string RedactApiKey(string? message)
    {
        if (string.IsNullOrEmpty(message) || string.IsNullOrWhiteSpace(_settings.ApiKey))
            return message ?? string.Empty;

        return message.Replace(_settings.ApiKey, "[REDACTED]", StringComparison.Ordinal);
    }

    // --- Modelos DTO internos para la API REST de Gemini ---

    private sealed class GeminiEmbedRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public GeminiContent Content { get; set; } = new();

        [JsonPropertyName("outputDimensionality")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? OutputDimensionality { get; set; }
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
