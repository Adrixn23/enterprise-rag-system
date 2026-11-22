using System.Diagnostics;
using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.Search;
using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Application.DTOs.Search;
using EnterpriseRag.Core.Domain.Observability;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Search;
using EnterpriseRag.Core.Domain.Interfaces.Documents;
using Microsoft.Extensions.Logging;

namespace EnterpriseRag.Core.Application.Services.Search;

public class SearchService : ISearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        IDocumentRepository documentRepository,
        ILogger<SearchService> logger)
    {
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<Result<List<SearchResultDto>>> SearchAsync(
        SearchQueryRequestDto request,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // 1. Validaciones defensivas de dominio
        if (tenantId == Guid.Empty)
        {
            return Result<List<SearchResultDto>>.Failure(SearchErrors.InvalidTenantId);
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Result<List<SearchResultDto>>.Failure(SearchErrors.EmptyQuery);
        }

        var trimmedQuery = request.Query.Trim();
        if (trimmedQuery.Length < 2)
        {
            return Result<List<SearchResultDto>>.Failure(SearchErrors.QueryTooShort(2));
        }

        if (request.Limit <= 0 || request.Limit > 50)
        {
            return Result<List<SearchResultDto>>.Failure(SearchErrors.InvalidLimit);
        }

        if (request.MinScore < 0.0f || request.MinScore > 1.0f)
        {
            return Result<List<SearchResultDto>>.Failure(SearchErrors.InvalidMinScore);
        }

        using var activity = RagTelemetry.ActivitySource.StartActivity("rag.retrieval");
        var sw = Stopwatch.StartNew();

        try
        {
            // 2. Vectorizar la consulta usando Gemini (text-embedding-004)
            var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(trimmedQuery, cancellationToken);
            if (embeddingResult.IsFailure)
            {
                _logger.LogError("Fallo al generar embedding para la consulta de búsqueda.");
                return Result<List<SearchResultDto>>.Failure(embeddingResult.Errors.ToArray());
            }

            // 3. Buscar vectores similares en Qdrant filtrando por TenantId
            var vectorSearchResult = await _vectorStoreService.SearchAsync(
                queryVector: embeddingResult.Value,
                tenantId: tenantId,
                limit: request.Limit,
                cancellationToken: cancellationToken);

            if (vectorSearchResult.IsFailure)
            {
                _logger.LogError("Fallo en la búsqueda de similitud en Qdrant para TenantId: {TenantId}", tenantId);
                return Result<List<SearchResultDto>>.Failure(vectorSearchResult.Errors.ToArray());
            }

            var vectorPoints = vectorSearchResult.Value ?? [];

            // 4. Filtrar por umbral mínimo de similitud (MinScore)
            var filteredPoints = vectorPoints
                .Where(p => p.Score >= request.MinScore)
                .ToList();

            if (filteredPoints.Count == 0)
            {
                return Result<List<SearchResultDto>>.Success([]);
            }

            // 5. Enriquecer los resultados en lote (Batch Query - Cero consultas N+1) y descartar huérfanos/eliminados
            var documentIds = filteredPoints
                .Select(p => p.DocumentId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var documentNames = documentIds.Count > 0
                ? await _documentRepository.GetFileNamesByIdsAsync(documentIds, tenantId, cancellationToken)
                : new Dictionary<Guid, string>();

            var results = new List<SearchResultDto>(filteredPoints.Count);
            var excludedOrphanCount = 0;

            foreach (var p in filteredPoints)
            {
                if (p.DocumentId == Guid.Empty ||
                    !documentNames.TryGetValue(p.DocumentId, out var fileName) ||
                    string.IsNullOrWhiteSpace(fileName))
                {
                    excludedOrphanCount++;
                    continue;
                }

                results.Add(new SearchResultDto
                {
                    PointId = p.PointId,
                    DocumentId = p.DocumentId,
                    DocumentFileName = fileName,
                    ChunkIndex = p.ChunkIndex,
                    Content = p.Content ?? string.Empty,
                    Score = p.Score
                });
            }

            if (excludedOrphanCount > 0)
            {
                _logger.LogWarning(
                    "Se excluyeron {Count} puntos vectoriales huérfanos o de documentos eliminados para TenantId: {TenantId}.",
                    excludedOrphanCount,
                    tenantId);
            }

            return Result<List<SearchResultDto>>.Success(results);
        }
        finally
        {
            RagTelemetry.RetrievalDurationMs.Record(sw.Elapsed.TotalMilliseconds);
        }
    }
}
