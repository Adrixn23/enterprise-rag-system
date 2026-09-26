

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseRag.Core.Application.Contracts.TextChunker
{
    public interface ITextChunkerService
    {
        List<string> ChunkText(string text, int chunkSizeInTokens = 500, int overlapTokens = 50);

    }
}
