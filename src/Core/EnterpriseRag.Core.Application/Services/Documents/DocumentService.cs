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
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentRepository documentRepository,
        IDocumentChunkRepository documentChunkRepository,
        IFileStorageService fileStorageService,
        ITextExtractorService textExtractorService,
        ITextChunkerService textChunkerService,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        ILogger<DocumentService> logger)
    {
        _documentRepository = documentRepository;
        _documentChunkRepository = documentChunkRepository;
        _fileStorageService = fileStorageService;
        _textExtractorService = textExtractorService;
        _textChunkerService = textChunkerService;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _logger = logger;
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

        // Validacion defensiva profunda (Magic Bytes): verificar que el contenido real coincida con el tipo declarado
        using (var validationStream = request.File.OpenReadStream())
        {
            if (!_textExtractorService.IsValidFileContent(validationStream, request.File.ContentType))
            {
                _logger.LogWarning("Archivo rechazado: La firma binaria (Magic Bytes) de '{FileName}' no coincide con el Content-Type declarado '{ContentType}'.",
                    request.File.FileName, request.File.ContentType);
                return Result<DocumentResponseDto>.Failure(DocumentErrors.UnsupportedFormat);
            }
        }

        // 2. Almacenamiento fisico inicial del archivo
        using var storageStream = request.File.OpenReadStream();
        var storagePath = await _fileStorageService.SaveFileAsync(storageStream, request.File.FileName, request.TenantId, cancellationToken);

        try
        {
            // 3. Extraccion de texto plano usando un segundo Stream independiente sin problemas de seek
            using var extractionStream = request.File.OpenReadStream();
            var textContent = await _textExtractorService.ExtractTextAsync(extractionStream, request.File.ContentType, cancellationToken);
            var rawChunks = _textChunkerService.ChunkText(textContent);

            // 4. Generacion de embeddings con Gemini en lote (Batch)
            var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(rawChunks, cancellationToken);
            if (embeddingResult.IsFailure)
            {
                // Compensacion: si falla la generacion de embeddings, limpiar el archivo fisico recien guardado
                await _fileStorageService.DeleteFileAsync(storagePath, cancellationToken);
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
                // Compensacion: si falla Qdrant, limpiar el archivo fisico en disco
                await _fileStorageService.DeleteFileAsync(storagePath, cancellationToken);
                return Result<DocumentResponseDto>.Failure(vectorStoreResult.Errors.ToArray());
            }

            // 8. Persistencia atomica en SQL Server con compensacion en caso de excepcion
            try
            {
                await _documentRepository.AddAsync(document, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo critico al persistir Documento en SQL Server tras haber guardado en Qdrant. Iniciando compensacion (Rollback).");

                // Compensacion distribuida (Saga): Borrar vectores en Qdrant y archivo fisico para no dejar huerfanos
                await _vectorStoreService.DeleteByDocumentAsync(document.Id, request.TenantId, cancellationToken);
                await _fileStorageService.DeleteFileAsync(storagePath, cancellationToken);

                return Result<DocumentResponseDto>.Failure(DocumentErrors.PersistenceFailed(ex.Message));
            }

            // 9. Retorno de respuesta exitosa mapeada a DTO con Mapster
            return Result<DocumentResponseDto>.Success(document.Adapt<DocumentResponseDto>());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Excepcion no controlada durante el procesamiento del documento. Limpiando almacenamiento fisico.");
            await _fileStorageService.DeleteFileAsync(storagePath, cancellationToken);
            throw;
        }
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

        var chunks = await _documentChunkRepository.GetByDocumentIdAsync(documentId, tenantId, cancellationToken);
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

        // 1. Eliminar vectores en Qdrant con validacion estricta de Result
        var vectorDeletionResult = await _vectorStoreService.DeleteByDocumentAsync(id, tenantId, cancellationToken);
        if (vectorDeletionResult.IsFailure)
        {
            _logger.LogError("Fallo al eliminar vectores en Qdrant para DocumentId: {DocumentId}. Se aborta la eliminacion en SQL para permitir reintentos.", id);
            return Result<bool>.Failure(vectorDeletionResult.Errors.ToArray());
        }

        // 2. Eliminar chunks en SQL Server
        await _documentChunkRepository.DeleteByDocumentIdAsync(id, tenantId, cancellationToken);

        // 3. Eliminar archivo fisico del almacenamiento
        await _fileStorageService.DeleteFileAsync(document.StoragePath, cancellationToken);

        // 4. Eliminar registro del documento en SQL Server
        await _documentRepository.DeleteAsync(document, cancellationToken);

        return Result<bool>.Success(true);
    }
}
