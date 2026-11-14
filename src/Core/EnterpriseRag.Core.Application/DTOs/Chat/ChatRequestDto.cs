namespace EnterpriseRag.Core.Application.DTOs.Chat;

public class ChatRequestDto
{
    public required string Message { get; set; }
    public int MaxContextChunks { get; set; } = 5;
}
