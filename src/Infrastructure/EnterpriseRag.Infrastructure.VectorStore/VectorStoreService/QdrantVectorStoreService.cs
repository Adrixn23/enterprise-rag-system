using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Application.DTOs.Vector;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.VectorStore;
using EnterpriseRag.Core.Domain.Settings;
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
        var pointsList = points?.ToList();
        if (pointsList is null || pointsList.Count == 0)
            return Result.Failure(VectorStoreErrors.EmptyPoints);

        try
        {
            await EnsureCollectionExistsAsync(cancellationToken);

            var qdrantPoints = pointsList.Select(p => new PointStruct
            {
                Id = p.PointId,
                Vectors = p.Vector.ToArray(),
                Payload =
                {
                    ["tenantId"] = p.TenantId.ToString(),
                    ["documentId"] = p.DocumentId.ToString(),
                    ["chunkIndex"] = p.ChunkIndex,
                    ["content"] = p.Content ?? string.Empty
                }
            }).ToList();

            await _client.UpsertAsync(_settings.CollectionName, qdrantPoints, cancellationToken: cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al insertar puntos en Qdrant.");
            return Result.Failure(VectorStoreErrors.UpsertFailed(ex.Message));
        }
    }

    private async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken)
    {
        var exists = await _client.CollectionExistsAsync(_settings.CollectionName, cancellationToken);
        if (!exists)
        {
            await _client.CreateCollectionAsync(
                collectionName: _settings.CollectionName,
                vectorsConfig: new VectorParams
                {
                    Size = 768,
                    Distance = Distance.Cosine
                },
                cancellationToken: cancellationToken);

            await _client.CreatePayloadIndexAsync(
                collectionName: _settings.CollectionName,
                fieldName: "tenantId",
                schemaType: PayloadSchemaType.Keyword,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Coleccion {Collection} creada exitosamente en Qdrant.", _settings.CollectionName);
        }
    }

    public async Task<Result<List<VectorSearchResultDto>>> SearchAsync(ReadOnlyMemory<float> queryVector, Guid tenantId, int limit = 5, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<Result> DeleteByDocumentAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
