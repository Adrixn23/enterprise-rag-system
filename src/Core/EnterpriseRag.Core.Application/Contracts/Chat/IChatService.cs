using EnterpriseRag.Core.Application.DTOs.Chat;
using EnterpriseRag.Core.Domain.Common;

namespace EnterpriseRag.Core.Application.Contracts.Chat;

public interface IChatService
{
    Task<Result<ChatResponseDto>> AskAsync(
        ChatRequestDto request,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
