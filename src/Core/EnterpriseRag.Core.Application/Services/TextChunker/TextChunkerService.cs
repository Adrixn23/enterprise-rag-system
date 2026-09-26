using EnterpriseRag.Core.Application.Contracts.TextChunker;

namespace EnterpriseRag.Core.Application.Services.TextChunker;

public class TextChunkerService : ITextChunkerService
{
    public List<string> ChunkText(string text, int chunkSizeInTokens = 500, int overlapTokens = 50)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        if (chunkSizeInTokens <= 0)
        {
            chunkSizeInTokens = 500;
        }

        if (overlapTokens < 0 || overlapTokens >= chunkSizeInTokens)
        {
            overlapTokens = Math.Max(0, chunkSizeInTokens / 10);
        }

        var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length <= chunkSizeInTokens)
        {
            return new List<string> { text.Trim() };
        }

        var chunks = new List<string>();
        int stepSize = chunkSizeInTokens - overlapTokens;

        for (int i = 0; i < words.Length; i += stepSize)
        {
            int count = Math.Min(chunkSizeInTokens, words.Length - i);
            var chunk = string.Join(" ", words[i..(i + count)]);

            chunks.Add(chunk);

            if (i + count >= words.Length)
            {
                break;
            }
        }

        return chunks;
    }
}
