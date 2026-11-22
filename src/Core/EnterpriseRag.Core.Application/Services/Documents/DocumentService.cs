using System.Security.Cryptography;
using EnterpriseRag.Core.Application.Contracts.Documents;
using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.FileService;
using EnterpriseRag.Core.Application.Contracts.TextChunker;
using EnterpriseRag.Core.Application.Contracts.TextExtraction;
using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Application.DTOs.Common;
using EnterpriseRag.Core.Application.DTOs.Documents;
using EnterpriseRag.Core.Application.DTOs.Vector;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Documents;
using EnterpriseRag.Core.Domain.Entities.Documents;
using EnterpriseRag.Core.Domain.Entities.Outbox;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Interfaces.Documents;
using EnterpriseRag.Core.Domain.Interfaces.Outbox;
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
    private readonly IOutboxRepository? _outboxRepository;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentRepository documentRepository,
        IDocumentChunkRepository documentChunkRepository,
        IFileStorageService fileStorageService,
        ITextExtractorService textExtractorService,
        ITextChunkerService textChunkerService,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        ILogger<DocumentService> logger,
        IOutboxRepository? outboxRepository = null)
    {
        _documentRepository = documentRepository;
        _documentChunkRepository = documentChunkRepository;
        _fileStorageService = fileStorageService;
        _textExtractorService = textExtractorService;
        _textChunkerService = textChunkerService;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _logger = logger;
        _outboxRepository = outboxRepository;
    }

    public async Task<Result<DocumentResponseDto>> EnqueueDocumentAsync(UploadDocumentRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.OpenReadStream == null || request.FileSizeBytes <= 0)
        {
            return Result<DocumentResponseDto>.Failure(DocumentErrors.EmptyFile);
        }

        if (!_textExtractorService.CanExtract(request.ContentType))
        {
            return Result<DocumentResponseDto>.Failure(DocumentErrors.UnsupportedFormat);
        }

        using (var validationStream = request.OpenReadStream())
        {
            if (!_textExtractorService.IsValidFileContent(validationStream, request.ContentType))
            {
                _logger.LogWarning("Archivo rechazado: La firma binaria (Magic Bytes) de '{FileName}' no coincide con el Content-Type declarado '{ContentType}'.",
                    request.FileName, request.ContentType);
                return Result<DocumentResponseDto>.Failure(DocumentErrors.UnsupportedFormat);
            }
        }

        string fileHash;
        using (var hashStream = request.OpenReadStream())
        {
            var hashBytes = await SHA256.HashDataAsync(hashStream, cancellationToken);
            fileHash = Convert.ToHexStringLower(hashBytes);
        }

        var existingDoc = await _documentRepository.GetByHashAsync(request.TenantId, fileHash, cancellationToken);
        if (existingDoc != null)
        {
            return Result<DocumentResponseDto>.Failure(DocumentErrors.DuplicateDocument);
        }

        using var storageStream = request.OpenReadStream();
        var storagePath = await _fileStorageService.SaveFileAsync(storageStream, request.FileName, request.TenantId, cancellationToken);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            FileHash = fileHash,
            StoragePath = storagePath,
            TenantId = request.TenantId,
            Status = DocumentProcessingStatus.Pending
        };

        var outboxMessage = new OutboxMessage
        {
            TenantId = request.TenantId,
            AggregateId = document.Id,
            EventType = OutboxEventTypes.IngestDocument
        };

        try
        {
            await _documentRepository.AddWithOutboxAsync(document, outboxMessage, cancellationToken);
            return Result<DocumentResponseDto>.Success(document.Adapt<DocumentResponseDto>());
        }
        catch (Exception ex)
        {
            await _fileStorageService.DeleteFileAsync(storagePath, CancellationToken.None);

            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            if (innerMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                innerMessage.Contains("unique", StringComparison.OrdinalIgnoreCase))
            {
                return Result<DocumentResponseDto>.Failure(DocumentErrors.DuplicateDocument);
            }

            return Result<DocumentResponseDto>.Failure(DocumentErrors.PersistenceFailed(ex.Message));
        }
    }

    public async Task<Result> ProcessPendingDocumentAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document == null || document.TenantId != tenantId || document.IsDeleted)
        {
            return Result.Failure(DocumentErrors.NotFound);
        }

        // Idempotencia: si ya fue completado previamente, no reprocesar
        if (document.Status == DocumentProcessingStatus.Completed)
        {
            return Result.Success();
        }

        await _documentRepository.UpdateStatusAsync(document.Id, tenantId, DocumentProcessingStatus.Processing, null, cancellationToken);

        await using var fileStream = await _fileStorageService.GetFileAsync(document.StoragePath, cancellationToken);
        if (fileStream == null)
        {
            const string missingMsg = "Archivo físico no encontrado en almacenamiento para procesar.";
            await _documentRepository.UpdateStatusAsync(document.Id, tenantId, DocumentProcessingStatus.Failed, missingMsg, CancellationToken.None);
            return Result.Failure(DocumentErrors.StorageFailed(missingMsg));
        }

        var textContent = await _textExtractorService.ExtractTextAsync(fileStream, document.ContentType, cancellationToken);
        var rawChunks = _textChunkerService.ChunkText(textContent);

        var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(rawChunks, cancellationToken);
        if (embeddingResult.IsFailure)
        {
            var err = embeddingResult.Errors.FirstOrDefault()?.Description ?? "Fallo en generación de embeddings.";
            await _documentRepository.UpdateStatusAsync(document.Id, tenantId, DocumentProcessingStatus.Failed, err, CancellationToken.None);
            return Result.Failure(embeddingResult.Errors.ToArray());
        }

        var vectors = embeddingResult.Value!;
        var chunks = new List<DocumentChunk>(rawChunks.Count);
        var vectorPoints = new List<VectorPointDto>(rawChunks.Count);

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
                TenantId = tenantId
            });

            vectorPoints.Add(new VectorPointDto
            {
                PointId = pointId,
                Vector = vectors[i],
                TenantId = tenantId,
                DocumentId = document.Id,
                ChunkIndex = i,
                Content = rawChunks[i]
            });
        }

        var vectorStoreResult = await _vectorStoreService.UpsertPointAsync(vectorPoints, cancellationToken);
        if (vectorStoreResult.IsFailure)
        {
            var err = vectorStoreResult.Errors.FirstOrDefault()?.Description ?? "Fallo en upsert de Qdrant.";
            await _documentRepository.UpdateStatusAsync(document.Id, tenantId, DocumentProcessingStatus.Failed, err, CancellationToken.None);
            return Result.Failure(vectorStoreResult.Errors.ToArray());
        }

        try
        {
            await _documentRepository.CompleteIngestionAsync(document.Id, tenantId, chunks, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al completar ingestión en SQL para DocumentId {DocumentId}. Compensando Qdrant.", document.Id);
            var compensationResult = await _vectorStoreService.DeleteByDocumentAsync(document.Id, tenantId, CancellationToken.None);
            if (compensationResult.IsFailure && _outboxRepository is not null)
            {
                await _outboxRepository.AddAsync(new OutboxMessage
                {
                    TenantId = tenantId,
                    AggregateId = document.Id,
                    EventType = OutboxEventTypes.DeleteVectors
                }, CancellationToken.None);
            }

            await _documentRepository.UpdateStatusAsync(document.Id, tenantId, DocumentProcessingStatus.Failed, ex.Message, CancellationToken.None);
            return Result.Failure(DocumentErrors.PersistenceFailed(ex.Message));
        }
    }

    public async Task<Result<DocumentResponseDto>> UploadDocumentAsync(UploadDocumentRequestDto request, CancellationToken cancellationToken = default)
    {
        // 1. Guardias de validacion de archivo vacio y formato no soportado
        if (request.OpenReadStream == null || request.FileSizeBytes <= 0)
        {
            return Result<DocumentResponseDto>.Failure(DocumentErrors.EmptyFile);
        }

        if (!_textExtractorService.CanExtract(request.ContentType))
        {
            return Result<DocumentResponseDto>.Failure(DocumentErrors.UnsupportedFormat);
        }

        // Validacion defensiva profunda (Magic Bytes): verificar que el contenido real coincida con el tipo declarado
        using (var validationStream = request.OpenReadStream())
        {
            if (!_textExtractorService.IsValidFileContent(validationStream, request.ContentType))
            {
                _logger.LogWarning("Archivo rechazado: La firma binaria (Magic Bytes) de '{FileName}' no coincide con el Content-Type declarado '{ContentType}'.",
                    request.FileName, request.ContentType);
                return Result<DocumentResponseDto>.Failure(DocumentErrors.UnsupportedFormat);
            }
        }

        // Deduplicacion SHA-256 estrictamente particionada por TenantId
        string fileHash;
        using (var hashStream = request.OpenReadStream())
        {
            var hashBytes = await SHA256.HashDataAsync(hashStream, cancellationToken);
            fileHash = Convert.ToHexStringLower(hashBytes);
        }

        var existingDoc = await _documentRepository.GetByHashAsync(request.TenantId, fileHash, cancellationToken);
        if (existingDoc != null)
        {
            _logger.LogInformation("Documento duplicado detectado por SHA-256 para TenantId {TenantId}.", request.TenantId);
            return Result<DocumentResponseDto>.Failure(DocumentErrors.DuplicateDocument);
        }

        // 2. Almacenamiento fisico inicial del archivo
        using var storageStream = request.OpenReadStream();
        var storagePath = await _fileStorageService.SaveFileAsync(storageStream, request.FileName, request.TenantId, cancellationToken);

        try
        {
            // 3. Extraccion de texto plano usando un segundo Stream independiente sin problemas de seek
            using var extractionStream = request.OpenReadStream();
            var textContent = await _textExtractorService.ExtractTextAsync(extractionStream, request.ContentType, cancellationToken);
            var rawChunks = _textChunkerService.ChunkText(textContent);

            // 4. Generacion de embeddings con Gemini en lote (Batch)
            var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(rawChunks, cancellationToken);
            if (embeddingResult.IsFailure)
            {
                // Compensacion: si falla la generacion de embeddings, limpiar el archivo fisico recien guardado
                await _fileStorageService.DeleteFileAsync(storagePath, CancellationToken.None);
                return Result<DocumentResponseDto>.Failure(embeddingResult.Errors.ToArray());
            }

            var vectors = embeddingResult.Value!;

            // 5. Construccion de la entidad Document principal (mismo criterio de identidad que EnqueueDocumentAsync)
            var document = new Document
            {
                Id = Guid.NewGuid(),
                FileName = request.FileName,
                ContentType = request.ContentType,
                FileSizeBytes = request.FileSizeBytes,
                FileHash = fileHash,
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
                await _vectorStoreService.DeleteByDocumentAsync(document.Id, request.TenantId, CancellationToken.None);
                await _fileStorageService.DeleteFileAsync(storagePath, CancellationToken.None);

                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                if (innerMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                    innerMessage.Contains("unique", StringComparison.OrdinalIgnoreCase))
                {
                    return Result<DocumentResponseDto>.Failure(DocumentErrors.DuplicateDocument);
                }

                return Result<DocumentResponseDto>.Failure(DocumentErrors.PersistenceFailed(ex.Message));
            }

            // 9. Retorno de respuesta exitosa mapeada a DTO con Mapster
            return Result<DocumentResponseDto>.Success(document.Adapt<DocumentResponseDto>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Procesamiento del documento cancelado por el cliente. Limpiando almacenamiento fisico.");
            await _fileStorageService.DeleteFileAsync(storagePath, CancellationToken.None);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Excepcion no controlada durante el procesamiento del documento. Limpiando almacenamiento fisico.");
            await _fileStorageService.DeleteFileAsync(storagePath, CancellationToken.None);
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

    public async Task<Result<PagedResponseDto<DocumentResponseDto>>> GetPagedByTenantAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await _documentRepository.GetPagedByTenantIdAsync(
            tenantId,
            safePage,
            safePageSize,
            cancellationToken);

        var dtos = items.Adapt<IReadOnlyList<DocumentResponseDto>>();

        return Result<PagedResponseDto<DocumentResponseDto>>.Success(new PagedResponseDto<DocumentResponseDto>
        {
            Items = dtos,
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount
        });
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
