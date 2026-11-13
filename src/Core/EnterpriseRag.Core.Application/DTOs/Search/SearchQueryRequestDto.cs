namespace EnterpriseRag.Core.Application.DTOs.Search;

public class SearchQueryRequestDto
{
    public required string Query { get; set; }
    public int Limit { get; set; } = 5;
    public float MinScore { get; set; } = 0.5f;
}
