using System.Diagnostics;
using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Application.DTOs.Vector;
using EnterpriseRag.Core.Domain.Observability;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Documents;
using EnterpriseRag.Core.Domain.Common.Errors.VectorStore;
using EnterpriseRag.Core.Domain.Settings;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace EnterpriseRag.Infrastructure.VectorStore.VectorStoreService;

public class QdrantVectorStoreService : IVectorStoreService
{
    private readonly QdrantClient _client;
    private readonly QdrantSettings _settings;
    private readonly ILogger<QdrantVectorStoreService> _logger;
    private readonly SemaphoreSlim _collectionLock = new(1, 1);
    private volatile bool _collectionVerified;

    public QdrantVectorStoreService(
        QdrantClient client,
        IOptions<QdrantSettings> options,
        ILogger<QdrantVectorStoreService> logger)
    {
        _client = client;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<Result> UpsertPointAsync(IEnumerable<VectorPointDto> points, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pointsList = points?.ToList();
        if (pointsList is null || pointsList.Count == 0)
            return Result.Failure(VectorStoreErrors.EmptyPoints);

        using var activity = RagTelemetry.ActivitySource.StartActivity("qdrant.upsert");
        var sw = Stopwatch.StartNew();

        try
        {
            await ExecuteWithTransientRetryAsync(async ct =>
            {
                await EnsureCollectionExistsAsync(ct);

                var qdrantPoints = pointsList.Select(p => new PointStruct
                {
                    Id = p.PointId,
                    Vectors = p.Vector.ToArray(),
                    Payload =
                    {
                        ["tenantId"]    = p.TenantId.ToString(),
                        ["documentId"]  = p.DocumentId.ToString(),
                        ["chunkIndex"]  = p.ChunkIndex,
                        ["content"]     = p.Content ?? string.Empty
                    }
                }).ToList();

                await _client.UpsertAsync(_settings.CollectionName, qdrantPoints, cancellationToken: ct);
            }, cancellationToken);

            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Operacion Upsert en Qdrant cancelada por el cliente.");
            throw;
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Operacion Upsert en Qdrant cancelada por el cliente durante llamada gRPC.");
            throw new OperationCanceledException("Operacion Upsert en Qdrant cancelada por el cliente.", ex, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout al insertar puntos en Qdrant.");
            RagTelemetry.ExternalErrors.Add(1, new KeyValuePair<string, object?>("dependency", "qdrant"), new KeyValuePair<string, object?>("operation", "upsert"));
            return Result.Failure(VectorStoreErrors.UpsertFailed("Timeout al comunicarse con Qdrant."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al insertar puntos en Qdrant.");
            RagTelemetry.ExternalErrors.Add(1, new KeyValuePair<string, object?>("dependency", "qdrant"), new KeyValuePair<string, object?>("operation", "upsert"));
            return Result.Failure(VectorStoreErrors.UpsertFailed(ex.Message));
        }
        finally
        {
            RagTelemetry.QdrantDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("operation", "upsert"));
        }
    }

    public async Task<Result<List<VectorSearchResultDto>>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        Guid tenantId,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (queryVector.IsEmpty)
            return Result<List<VectorSearchResultDto>>.Failure(VectorStoreErrors.EmptyQueryVector);

        if (tenantId == Guid.Empty)
            return Result<List<VectorSearchResultDto>>.Failure(VectorStoreErrors.InvalidTenantId);

        using var activity = RagTelemetry.ActivitySource.StartActivity("qdrant.search");
        var sw = Stopwatch.StartNew();

        try
        {
            return await ExecuteWithTransientRetryAsync(async ct =>
            {
                if (!_collectionVerified)
                {
                    var exists = await _client.CollectionExistsAsync(_settings.CollectionName, ct);
                    if (!exists)
                    {
                        _logger.LogWarning("Busqueda solicitada pero la coleccion {Collection} no existe en Qdrant.", _settings.CollectionName);
                        return Result<List<VectorSearchResultDto>>.Success([]);
                    }

                    _collectionVerified = true;
                }

                var filter = BuildTenantSearchFilter(tenantId);

                var searchResults = await _client.SearchAsync(
                    collectionName: _settings.CollectionName,
                    vector: queryVector.ToArray(),
                    filter: filter,
                    limit: (ulong)limit,
                    cancellationToken: ct);

                var dtoList = searchResults.Select(point => new VectorSearchResultDto
                {
                    PointId     = Guid.Parse(point.Id.Uuid),
                    DocumentId  = Guid.Parse(point.Payload["documentId"].StringValue),
                    ChunkIndex  = (int)point.Payload["chunkIndex"].IntegerValue,
                    Content     = point.Payload.TryGetValue("content", out var contentVal) ? contentVal.StringValue : string.Empty,
                    Score       = point.Score
                }).ToList();

                return Result<List<VectorSearchResultDto>>.Success(dtoList);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Busqueda vectorial en Qdrant cancelada por el cliente para TenantId: {TenantId}", tenantId);
            throw;
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Busqueda vectorial en Qdrant cancelada por el cliente durante llamada gRPC para TenantId: {TenantId}", tenantId);
            throw new OperationCanceledException("Busqueda vectorial en Qdrant cancelada por el cliente.", ex, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout al realizar la busqueda vectorial en Qdrant para TenantId: {TenantId}", tenantId);
            RagTelemetry.ExternalErrors.Add(1, new KeyValuePair<string, object?>("dependency", "qdrant"), new KeyValuePair<string, object?>("operation", "search"));
            return Result<List<VectorSearchResultDto>>.Failure(VectorStoreErrors.SearchFailed("Timeout al comunicarse con Qdrant."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al realizar la busqueda vectorial en Qdrant para TenantId: {TenantId}", tenantId);
            RagTelemetry.ExternalErrors.Add(1, new KeyValuePair<string, object?>("dependency", "qdrant"), new KeyValuePair<string, object?>("operation", "search"));
            return Result<List<VectorSearchResultDto>>.Failure(VectorStoreErrors.SearchFailed(ex.Message));
        }
        finally
        {
            RagTelemetry.QdrantDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("operation", "search"));
        }
    }

    public async Task<Result> DeleteByDocumentAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (documentId == Guid.Empty)
            return Result.Failure(DocumentErrors.NotFound);

        if (tenantId == Guid.Empty)
            return Result.Failure(VectorStoreErrors.InvalidTenantId);

        using var activity = RagTelemetry.ActivitySource.StartActivity("qdrant.delete");
        var sw = Stopwatch.StartNew();

        try
        {
            await ExecuteWithTransientRetryAsync(async ct =>
            {
                var filter = BuildDocumentDeleteFilter(documentId, tenantId);

                await _client.DeleteAsync(
                    collectionName: _settings.CollectionName,
                    filter: filter,
                    cancellationToken: ct);
            }, cancellationToken);

            _logger.LogInformation(
                "Vectores eliminados en Qdrant para DocumentId: {DocumentId}, TenantId: {TenantId}",
                documentId, tenantId);

            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Eliminacion en Qdrant cancelada por el cliente para DocumentId: {DocumentId}, TenantId: {TenantId}",
                documentId, tenantId);
            throw;
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Eliminacion en Qdrant cancelada por el cliente durante llamada gRPC para DocumentId: {DocumentId}, TenantId: {TenantId}",
                documentId, tenantId);
            throw new OperationCanceledException("Eliminacion en Qdrant cancelada por el cliente.", ex, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError(
                "Timeout al eliminar vectores en Qdrant para DocumentId: {DocumentId}, TenantId: {TenantId}",
                documentId, tenantId);
            RagTelemetry.ExternalErrors.Add(1, new KeyValuePair<string, object?>("dependency", "qdrant"), new KeyValuePair<string, object?>("operation", "delete"));
            return Result.Failure(VectorStoreErrors.DeleteFailed("Timeout al comunicarse con Qdrant."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al eliminar vectores en Qdrant para DocumentId: {DocumentId}, TenantId: {TenantId}",
                documentId, tenantId);
            RagTelemetry.ExternalErrors.Add(1, new KeyValuePair<string, object?>("dependency", "qdrant"), new KeyValuePair<string, object?>("operation", "delete"));
            return Result.Failure(VectorStoreErrors.DeleteFailed(ex.Message));
        }
        finally
        {
            RagTelemetry.QdrantDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("operation", "delete"));
        }
    }

    private async Task ExecuteWithTransientRetryAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await ExecuteWithTransientRetryAsync<object?>(async ct =>
        {
            await action(ct);
            return null;
        }, cancellationToken);
    }

    private async Task<T> ExecuteWithTransientRetryAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 2;
        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await action(cancellationToken);
            }
            catch (RpcException ex) when (attempt < maxRetries && IsTransientGrpcStatus(ex.StatusCode) && !cancellationToken.IsCancellationRequested)
            {
                var delayMs = (int)(Math.Pow(2, attempt) * 200) + Random.Shared.Next(0, 100);
                _logger.LogWarning(ex, "Error transitorio en Qdrant ({StatusCode}). Reintento {Attempt}/{MaxRetries} en {DelayMs} ms.",
                    ex.StatusCode, attempt + 1, maxRetries, delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }
        }
    }

    private static bool IsTransientGrpcStatus(StatusCode statusCode) =>
        statusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded or StatusCode.ResourceExhausted;

    // -----------------------------------------------------------
    // Helper privado: crea la coleccion si no existe (cacheado tras verificar)
    // -----------------------------------------------------------
    private async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken)
    {
        if (_collectionVerified)
        {
            return;
        }

        await _collectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_collectionVerified)
            {
                return;
            }

            var exists = await _client.CollectionExistsAsync(_settings.CollectionName, cancellationToken);
            if (!exists)
            {
                var dimension = _settings.VectorDimension > 0 ? (ulong)_settings.VectorDimension : 768UL;

                await _client.CreateCollectionAsync(
                    collectionName: _settings.CollectionName,
                    vectorsConfig: new VectorParams
                    {
                        Size     = dimension,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: cancellationToken);

                // Indice de payload por tenantId para filtrado rapido multitenant
                await _client.CreatePayloadIndexAsync(
                    collectionName: _settings.CollectionName,
                    fieldName:  "tenantId",
                    schemaType: PayloadSchemaType.Keyword,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Coleccion {Collection} creada exitosamente en Qdrant (dimensión {Dimension}).",
                    _settings.CollectionName, dimension);
            }

            _collectionVerified = true;
        }
        finally
        {
            _collectionLock.Release();
        }
    }

    public static Filter BuildTenantSearchFilter(Guid tenantId) =>
        new()
        {
            Must = { Conditions.MatchKeyword("tenantId", tenantId.ToString()) }
        };

    public static Filter BuildDocumentDeleteFilter(Guid documentId, Guid tenantId) =>
        new()
        {
            Must =
            {
                Conditions.MatchKeyword("tenantId", tenantId.ToString()),
                Conditions.MatchKeyword("documentId", documentId.ToString())
            }
        };
}
