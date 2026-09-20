using EnterpriseRag.Core.Domain.Enums;

namespace EnterpriseRag.Core.Application.DTOs.Documents;

public class DocumentResponseDto
{
    public Guid Id { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public required string StoragePath { get; set; }
    public required string TenantId { get; set; }
    public DocumentProcessingStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int TotalChunks { get; set; }
}
