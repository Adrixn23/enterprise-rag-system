namespace EnterpriseRag.WebApi.Controllers;

using EnterpriseRag.Core.Application.Contracts.Chat;
using EnterpriseRag.Core.Application.DTOs.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Ask(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveTenantId(out var tenantId))
        {
            return Unauthorized();
        }

        var result = await _chatService.AskAsync(request, tenantId, cancellationToken);

        if (!result.IsSuccess)
        {
            var firstError = result.Errors.FirstOrDefault();
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = firstError?.Code ?? "Chat.Failed",
                Detail = firstError?.Description ?? "Ocurrió un error al procesar la consulta en el asistente de IA."
            });
        }

        return Ok(result.Value);
    }

    private bool TryResolveTenantId(out Guid tenantId)
    {
        var tenantClaim = User.FindFirst("tenantId")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantClaim) && Guid.TryParse(tenantClaim, out tenantId) && tenantId != Guid.Empty)
        {
            return true;
        }

        tenantId = Guid.Empty;
        return false;
    }
}
