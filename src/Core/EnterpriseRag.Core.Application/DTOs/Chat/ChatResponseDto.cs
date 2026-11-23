namespace EnterpriseRag.Core.Application.DTOs.Chat;

public class ChatResponseDto
{
    public required string Answer { get; set; }
    public List<ChatSourceDto> Sources { get; set; } = [];
}

public class ChatSourceDto
{
    public Guid DocumentId { get; set; }
    public string DocumentFileName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public float RelevanceScore { get; set; }
}
