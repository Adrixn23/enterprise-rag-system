using EnterpriseRag.Core.Application.DTOs.Documents;
using EnterpriseRag.Core.Domain.Common;

namespace EnterpriseRag.Core.Application.Contracts.Documents;

public interface IDocumentService
{
    Task<Result<DocumentResponseDto>> UploadDocumentAsync(UploadDocumentRequestDto request, CancellationToken cancellationToken = default);
    Task<Result<DocumentResponseDto>> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<DocumentResponseDto>>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<DocumentChunkResponseDto>>> GetChunksByDocumentIdAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteDocumentAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
