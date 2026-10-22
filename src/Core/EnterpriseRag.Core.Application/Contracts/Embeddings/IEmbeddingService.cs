using EnterpriseRag.Core.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseRag.Core.Application.Contracts.Embeddings
{
    public interface IEmbeddingService
    {
        Task<Result<ReadOnlyMemory<float>>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

        Task<Result<IReadOnlyList<ReadOnlyMemory<float>>>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default); 

    }
}
