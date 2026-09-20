namespace EnterpriseRag.Core.Application.DTOs.Documents;

public class DocumentChunkResponseDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public required string Content { get; set; }
    public int TokenCount { get; set; }
    public required string VectorId { get; set; }
    public required string TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
