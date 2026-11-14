namespace EnterpriseRag.Core.Domain.Settings;

public class GeminiSettings
{
    public const string SectionName = "GeminiSettings";

    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "text-embedding-004";
    public int EmbeddingDimension { get; set; } = 768;
    public string GenerationModelId { get; set; } = "gemini-1.5-flash";
}
