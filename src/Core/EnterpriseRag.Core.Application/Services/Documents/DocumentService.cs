using EnterpriseRag.Core.Application.Contracts.Documents;
using EnterpriseRag.Core.Application.Contracts.FileService;
using EnterpriseRag.Core.Application.Contracts.TextChunker;
using EnterpriseRag.Core.Application.Contracts.TextExtraction;
using EnterpriseRag.Core.Application.DTOs.Documents;
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

    public DocumentService(
        IDocumentRepository documentRepository,
        IDocumentChunkRepository documentChunkRepository,
        IFileStorageService fileStorageService,
        ITextExtractorService textExtractorService,
        ITextChunkerService textChunkerService)
    {
        _documentRepository = documentRepository;
        _documentChunkRepository = documentChunkRepository;
        _fileStorageService = fileStorageService;
        _textExtractorService = textExtractorService;
        _textChunkerService = textChunkerService;
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

        // 4. Construccion de la entidad Document principal en estado Completed
        var document = new Document
        {
            FileName = request.File.FileName,
            ContentType = request.File.ContentType,
            FileSizeBytes = request.File.Length,
            StoragePath = storagePath,


            TenantId = request.TenantId,
            Status = DocumentProcessingStatus.Completed
        };

        // 5. Generacion de la coleccion de entidades DocumentChunk vinculadas
        var chunks = new List<DocumentChunk>();
        for (int i = 0; i < rawChunks.Count; i++)
        {
            chunks.Add(new DocumentChunk
            {
                DocumentId = document.Id,
                ChunkIndex = i,
                Content = rawChunks[i],
                TokenCount = rawChunks[i].Length / 4,
                VectorId = Guid.NewGuid().ToString(),
                TenantId = request.TenantId
            });
        }

        document.Chunks = chunks;

        // 6. Persistencia atomica en una sola transaccion EF Core
        await _documentRepository.AddAsync(document, cancellationToken);

        // 7. Retorno de respuesta exitosa mapeada a DTO con Mapster
        return Result<DocumentResponseDto>.Success(document.Adapt<DocumentResponseDto>());
    }

    public async Task<Result<DocumentResponseDto>> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);
        if (document == null || document.TenantId != tenantId)
        {
            return Result<DocumentResponseDto>.Failure(DocumentErrors.NotFound);
        }

        return Result<DocumentResponseDto>.Success(document.Adapt<DocumentResponseDto>());
       
    }

    public async Task<Result<IEnumerable<DocumentResponseDto>>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {

        var documents = await _documentRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var dtos = documents.Adapt<IEnumerable<DocumentResponseDto>>();

        return Result<IEnumerable<DocumentResponseDto>>.Success(dtos);
    }

    public async Task<Result<IEnumerable<DocumentChunkResponseDto>>> GetChunksByDocumentIdAsync(Guid documentId, string tenantId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId , cancellationToken);
        if (document == null || document.TenantId != tenantId)
        {
            return Result<IEnumerable<DocumentChunkResponseDto>>.Failure(DocumentErrors.NotFound);
        }

        var chunks = await _documentChunkRepository.GetByDocumentIdAsync(documentId, cancellationToken);

        var dtos = chunks.Adapt<IEnumerable<DocumentChunkResponseDto>>();

        return Result<IEnumerable<DocumentChunkResponseDto>>.Success(dtos);

        
    }

    public async Task<Result<bool>> DeleteDocumentAsync(Guid id, string tenantId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);

        if (document == null || document.TenantId != tenantId)
        {
            return Result<bool>.Failure(DocumentErrors.NotFound);
        }
        await _documentChunkRepository.DeleteByDocumentIdAsync(id, cancellationToken);

        await _fileStorageService.DeleteFileAsync(document.StoragePath, cancellationToken);

        await _documentRepository.DeleteAsync(document, cancellationToken);

        return Result<bool>.Success(true);

    }
}
