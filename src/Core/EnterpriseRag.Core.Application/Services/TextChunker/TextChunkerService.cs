using EnterpriseRag.Core.Application.Contracts.TextChunker;

namespace EnterpriseRag.Core.Application.Services.TextChunker;

public class TextChunkerService : ITextChunkerService
{
    public List<string> ChunkText(string text, int chunkSizeInTokens = 500, int overlapTokens = 50)
    {
        throw new NotImplementedException();
    }
}
