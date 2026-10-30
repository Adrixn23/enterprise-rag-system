namespace EnterpriseRag.Core.Application.DTOs.Vector;

public class VectorSearchResultDto
{
    public Guid PointId { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string? Content { get; set; }
    public float Score { get; set; }
}
