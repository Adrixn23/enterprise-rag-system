using EnterpriseRag.Core.Domain.Common;

namespace EnterpriseRag.Core.Application.Contracts.GenerativeAi;

public interface IGenerativeAiService
{
    Task<Result<string>> GenerateResponseAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}
