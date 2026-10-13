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

        var normalizedText = text.Replace("\r\n", "\n").Replace('\r', '\n');

        var rawParagraphs = normalizedText
            .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        var semanticUnits = new List<string>();
        foreach (var paragraph in rawParagraphs)
        {
            int estimatedTokens = EstimateTokenCount(paragraph);
            if (estimatedTokens <= chunkSizeInTokens)
            {
                semanticUnits.Add(paragraph);
            }
            else
            {
                var sentences = SplitIntoSentences(paragraph);
                semanticUnits.AddRange(sentences);
            }
        }

        var chunks = new List<string>();
        var currentChunkUnits = new List<string>();
        int currentChunkTokens = 0;

        for (int i = 0; i < semanticUnits.Count; i++)
        {
            var unit = semanticUnits[i];
            int unitTokens = EstimateTokenCount(unit);

            if (unitTokens > chunkSizeInTokens)
            {
                if (currentChunkUnits.Count > 0)
                {
                    chunks.Add(string.Join("\n\n", currentChunkUnits));
                    currentChunkUnits.Clear();
                    currentChunkTokens = 0;
                }

                chunks.AddRange(ChunkWordsWithOverlap(unit, chunkSizeInTokens, overlapTokens));
                continue;
            }

            if (currentChunkTokens + unitTokens > chunkSizeInTokens && currentChunkUnits.Count > 0)
            {
                chunks.Add(string.Join("\n\n", currentChunkUnits));

                var overlapUnits = new List<string>();
                int overlapAcc = 0;
                for (int j = currentChunkUnits.Count - 1; j >= 0; j--)
                {
                    int prevTokens = EstimateTokenCount(currentChunkUnits[j]);
                    if (overlapAcc + prevTokens <= overlapTokens)
                    {
                        overlapUnits.Insert(0, currentChunkUnits[j]);
                        overlapAcc += prevTokens;
                    }
                    else
                    {
                        break;
                    }
                }

                currentChunkUnits = new List<string>(overlapUnits);
                currentChunkTokens = overlapAcc;
            }

            currentChunkUnits.Add(unit);
            currentChunkTokens += unitTokens;
        }

        if (currentChunkUnits.Count > 0)
        {
            chunks.Add(string.Join("\n\n", currentChunkUnits));
        }

        return chunks;
    }

    private static int EstimateTokenCount(string text)
    {
        return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            current.Append(c);

            if ((c == '.' || c == '!' || c == '?' || c == '\n') && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
            {
                var sentence = current.ToString().Trim();
                if (!string.IsNullOrEmpty(sentence))
                {
                    sentences.Add(sentence);
                }
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            var sentence = current.ToString().Trim();
            if (!string.IsNullOrEmpty(sentence))
            {
                sentences.Add(sentence);
            }
        }

        return sentences;
    }

    private static List<string> ChunkWordsWithOverlap(string text, int chunkSizeInTokens, int overlapTokens)
    {
        var words = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        int wordsPerChunk = (int)(chunkSizeInTokens * 0.75);
        int wordsOverlap = (int)(overlapTokens * 0.75);
        int step = Math.Max(1, wordsPerChunk - wordsOverlap);

        for (int i = 0; i < words.Length; i += step)
        {
            int count = Math.Min(wordsPerChunk, words.Length - i);
            chunks.Add(string.Join(" ", words[i..(i + count)]));

            if (i + count >= words.Length)
            {
                break;
            }
        }

        return chunks;
    }
}
