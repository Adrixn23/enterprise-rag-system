using Microsoft.AspNetCore.Http;

namespace EnterpriseRag.Core.Application.DTOs.Documents;

public class UploadDocumentRequestDto
{
    public required IFormFile File { get; set; }
    public required string TenantId { get; set; }
}
