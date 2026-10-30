namespace EnterpriseRag.Core.Application.DTOs.Vector;

public class VectorPointDto
{
    public Guid PointId { get; set; }
    public ReadOnlyMemory<float> Vector { get; set; }
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string? Content { get; set; }
}
