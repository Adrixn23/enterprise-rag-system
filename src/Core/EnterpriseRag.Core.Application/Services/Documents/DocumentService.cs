using System;
using System.Threading;
using System.Threading.Tasks;
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

namespace EnterpriseRag.Core.Application.Services.Documents
{
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
            ITextChunkerService textChunkerService
            )
        {
            _documentRepository = documentRepository;
            _documentChunkRepository = documentChunkRepository;
            _fileStorageService = fileStorageService;
            _textExtractorService = textExtractorService;
            _textChunkerService = textChunkerService;
        }



        public async Task<Result<DocumentResponseDto>> UploadDocumentAsync(UploadDocumentRequestDto request, CancellationToken cancellationToken = default)
        {
            if (request.File == null || request.File.Length == 0 )
            {
                return Result<DocumentResponseDto>.Failure(DocumentErrors.EmptyFile);
            }
            if (_textExtractorService.CanExtract(request.File.ContentType))
            {
                return Result<DocumentResponseDto>.Failure(DocumentErrors.UnsupportedFormat);
            }

        }

        public Task<Result<DocumentResponseDto>> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<DocumentResponseDto>>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<DocumentChunkResponseDto>>> GetChunksByDocumentIdAsync(Guid documentId, string tenantId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> DeleteDocumentAsync(Guid id, string tenantId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
