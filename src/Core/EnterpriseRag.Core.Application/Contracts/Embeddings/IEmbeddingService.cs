using EnterpriseRag.Core.Domain.Common;

namespace EnterpriseRag.Core.Application.Contracts.Embeddings;

public interface IEmbeddingService
{
    // Genera un embedding para un texto individual
    Task<Result<ReadOnlyMemory<float>>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    // Genera embeddings en lote (batch) para multiples textos en una sola llamada a la API
    Task<Result<IReadOnlyList<ReadOnlyMemory<float>>>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
