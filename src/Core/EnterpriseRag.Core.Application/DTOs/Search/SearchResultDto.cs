namespace EnterpriseRag.Core.Application.DTOs.Search;

public class SearchResultDto
{
    public Guid PointId { get; set; }
    public Guid DocumentId { get; set; }
    public string? DocumentFileName { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public float Score { get; set; }
}
