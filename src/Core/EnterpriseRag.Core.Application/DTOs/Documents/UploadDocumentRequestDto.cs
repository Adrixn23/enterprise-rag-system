namespace EnterpriseRag.Core.Application.DTOs.Documents;

public class UploadDocumentRequestDto
{
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public required Func<Stream> OpenReadStream { get; set; }
    public Guid TenantId { get; set; }
}
