using EnterpriseRag.Core.Application.DTOs.Common;
using EnterpriseRag.Core.Application.DTOs.Documents;
using EnterpriseRag.Core.Domain.Common;

namespace EnterpriseRag.Core.Application.Contracts.Documents;

public interface IDocumentService
{
    Task<Result<DocumentResponseDto>> UploadDocumentAsync(UploadDocumentRequestDto request, CancellationToken cancellationToken = default);
    Task<Result<DocumentResponseDto>> EnqueueDocumentAsync(UploadDocumentRequestDto request, CancellationToken cancellationToken = default);
    Task<Result> ProcessPendingDocumentAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<DocumentResponseDto>> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<DocumentResponseDto>>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<PagedResponseDto<DocumentResponseDto>>> GetPagedByTenantAsync(Guid tenantId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<DocumentChunkResponseDto>>> GetChunksByDocumentIdAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteDocumentAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
