using EnterpriseRag.Core.Application.Contracts.Documents;
using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.FileService;
using EnterpriseRag.Core.Application.Contracts.TextChunker;
using EnterpriseRag.Core.Application.Contracts.TextExtraction;
using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Application.DTOs.Documents;
using EnterpriseRag.Core.Application.DTOs.Vector;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Documents;
using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Interfaces.Documents;
using Mapster;

namespace EnterpriseRag.Core.Application.Services.Documents;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentChunkRepository _documentChunkRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ITextExtractorService _textExtractorService;
    private readonly ITextChunkerService _textChunkerService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;

    public DocumentService(
        IDocumentRepository documentRepository,
        IDocumentChunkRepository documentChunkRepository,
        IFileStorageService fileStorageService,
        ITextExtractorService textExtractorService,
        ITextChunkerService textChunkerService,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService)
    {
        _documentRepository = documentRepository;
        _documentChunkRepository = documentChunkRepository;
        _fileStorageService = fileStorageService;
        _textExtractorService = textExtractorService;
        _textChunkerService = textChunkerService;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
    }

    public async Task<Result<DocumentResponseDto>> UploadDocumentAsync(UploadDocumentRequestDto request, CancellationToken cancellationToken = default)
    {
        // 1. Guardias de validacion de archivo vacio y formato no soportado
        if (request.File == null || request.File.Length == 0)
        {
            return Result<DocumentResponseDto>.Failure(DocumentErrors.EmptyFile);
        }

        if (!_textExtractorService.CanExtract(request.File.ContentType))
        {
            return Result<DocumentResponseDto>.Failure(DocumentErrors.UnsupportedFormat);
        }

        // 2. Almacenamiento fisico del archivo usando un Stream independiente
        using var storageStream = request.File.OpenReadStream();
        var storagePath = await _fileStorageService.SaveFileAsync(storageStream, request.File.FileName, request.TenantId, cancellationToken);

        // 3. Extraccion de texto plano usando un segundo Stream independiente sin problemas de seek
        using var extractionStream = request.File.OpenReadStream();
        var textContent = await _textExtractorService.ExtractTextAsync(extractionStream, request.File.ContentType, cancellationToken);
        var rawChunks = _textChunkerService.ChunkText(textContent);

        // 4. Generacion de embeddings con Gemini en lote (Batch)
        var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(rawChunks, cancellationToken);
        if (embeddingResult.IsFailure)
        {
            return Result<DocumentResponseDto>.Failure(embeddingResult.Errors.ToArray());
        }

        var vectors = embeddingResult.Value!;

        // 5. Construccion de la entidad Document principal
        var document = new Document
        {
            FileName = request.File.FileName,
            ContentType = request.File.ContentType,
            FileSizeBytes = request.File.Length,
            StoragePath = storagePath,
            TenantId = request.TenantId,
            Status = DocumentProcessingStatus.Completed
        };

        // 6. Generacion de Chunks para SQL y VectorPoints para Qdrant
        var chunks = new List<DocumentChunk>();
        var vectorPoints = new List<VectorPointDto>();

        for (int i = 0; i < rawChunks.Count; i++)
        {
            var pointId = Guid.NewGuid();

            chunks.Add(new DocumentChunk
            {
                DocumentId = document.Id,
                ChunkIndex = i,
                Content = rawChunks[i],
                TokenCount = rawChunks[i].Length / 4,
                VectorId = pointId.ToString(),
                TenantId = request.TenantId
            });

            vectorPoints.Add(new VectorPointDto
            {
                PointId = pointId,
                Vector = vectors[i],
                TenantId = request.TenantId,
                DocumentId = document.Id,
                ChunkIndex = i,
                Content = rawChunks[i]
            });
        }

        document.Chunks = chunks;

        // 7. Almacenar puntos vectoriales en Qdrant
        var vectorStoreResult = await _vectorStoreService.UpsertPointAsync(vectorPoints, cancellationToken);
        if (vectorStoreResult.IsFailure)
        {
            return Result<DocumentResponseDto>.Failure(vectorStoreResult.Errors.ToArray());
        }

        // 8. Persistencia atomica en SQL Server
        await _documentRepository.AddAsync(document, cancellationToken);

        // 9. Retorno de respuesta exitosa mapeada a DTO con Mapster
        return Result<DocumentResponseDto>.Success(document.Adapt<DocumentResponseDto>());
    }

    public async Task<Result<DocumentResponseDto>> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);
        if (document == null || document.TenantId != tenantId)
        {
            return Result<DocumentResponseDto>.Failure(DocumentErrors.NotFound);
        }

        return Result<DocumentResponseDto>.Success(document.Adapt<DocumentResponseDto>());
    }

    public async Task<Result<IEnumerable<DocumentResponseDto>>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var documents = await _documentRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var dtos = documents.Adapt<IEnumerable<DocumentResponseDto>>();

        return Result<IEnumerable<DocumentResponseDto>>.Success(dtos);
    }

    public async Task<Result<IEnumerable<DocumentChunkResponseDto>>> GetChunksByDocumentIdAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document == null || document.TenantId != tenantId)
        {
            return Result<IEnumerable<DocumentChunkResponseDto>>.Failure(DocumentErrors.NotFound);
        }

        var chunks = await _documentChunkRepository.GetByDocumentIdAsync(documentId, cancellationToken);
        var dtos = chunks.Adapt<IEnumerable<DocumentChunkResponseDto>>();

        return Result<IEnumerable<DocumentChunkResponseDto>>.Success(dtos);
    }

    public async Task<Result<bool>> DeleteDocumentAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);

        if (document == null || document.TenantId != tenantId)
        {
            return Result<bool>.Failure(DocumentErrors.NotFound);
        }

        // 1. Eliminar vectores en Qdrant
        await _vectorStoreService.DeleteByDocumentAsync(id, tenantId, cancellationToken);

        // 2. Eliminar chunks en SQL Server
        await _documentChunkRepository.DeleteByDocumentIdAsync(id, cancellationToken);

        // 3. Eliminar archivo fisico del almacenamiento
        await _fileStorageService.DeleteFileAsync(document.StoragePath, cancellationToken);

        // 4. Eliminar registro del documento en SQL Server
        await _documentRepository.DeleteAsync(document, cancellationToken);

        return Result<bool>.Success(true);
    }
}
