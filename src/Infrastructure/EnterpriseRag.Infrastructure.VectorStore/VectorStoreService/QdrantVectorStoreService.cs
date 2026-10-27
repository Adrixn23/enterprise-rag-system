using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Application.DTOs.Vector;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.VectorStore;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Qdrant.Client.Grpc.Qdrant;

namespace EnterpriseRag.Infrastructure.VectorStore.VectorStoreService
{
    public class QdrantVectorStoreService : IVectorStoreService
    {
        private readonly QdrantClient _client;
        private readonly QdrantSettings _settings;
        private readonly ILogger<QdrantVectorStoreService> _logger;

        public QdrantVectorStoreService(QdrantClient client, IOptions<QdrantSettings> options, ILogger<QdrantVectorStoreService> logger) {
            _client = client;
            _settings = options.Value;
            _logger = logger;


        }

        public async Task<Result> UpsertPointAsync(IEnumerable<VectorPointDto> points, CancellationToken cancellationToken = default) {
            var pointsList = points?.ToList();
            
            if (pointsList is null || pointsList.Count == 0)
            {
                return Result.Failure(VectorStoreErrors.EmptyPoints);
            }
            var collectionName = _settings.CollectionName;

            var exists = await _client.CollectionExistsAsync(_settings.CollectionName, cancellationToken);
            if (!exists)
            {
                await _client.CreateCollectionAsync(
                    collectionName: new VectorParams
                    {
                        Size = 768,
                        Distance = Distance.Cosine
                    },


                    cancellationToken: cancellationToken);

                await _client.CreatePayloadIndexAsync(
                    collectionName: _settings.CollectionName,
                    fieldName: "tenantId",
                    SchemaType: PayloadSchemaType.Keyword,
                    cancellationToken: cancellationToken


                    );

                _logger.LogInformation("Colección '{Collection}' creada exitosamente en Qdrant.", _settings.CollectionName);
            }
        }



        public async Task<Result<List<VectorSearchResultDto>>> SearchAsync(ReadOnlyMemory<float> queryVector, Guid tenantId, int limit = 5, CancellationToken cancellationToken = default) {

            throw new NotImplementedException();

        }

        public async Task<Result> DeleteByDocumentAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken = default) {

            throw new NotImplementedException();
        }


    }
}
