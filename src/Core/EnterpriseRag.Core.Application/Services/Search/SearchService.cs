using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.Search;
using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Application.DTOs.Search;
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

        // 5. Enriquecer los resultados con el nombre del documento original desde SQL
        var documentIds = filteredPoints.Select(p => p.DocumentId).Distinct().ToList();
        var documentNames = new Dictionary<Guid, string>();

        foreach (var docId in documentIds)
        {
            var doc = await _documentRepository.GetByIdAsync(docId, cancellationToken);
            if (doc != null)
            {
                documentNames[docId] = doc.FileName;
            }
        }

        var results = filteredPoints.Select(p => new SearchResultDto
        {
            PointId = p.PointId,
            DocumentId = p.DocumentId,
            DocumentFileName = documentNames.GetValueOrDefault(p.DocumentId, "Desconocido"),
            ChunkIndex = p.ChunkIndex,
            Content = p.Content ?? string.Empty,
            Score = p.Score
        }).ToList();

        return Result<List<SearchResultDto>>.Success(results);
    }
}
