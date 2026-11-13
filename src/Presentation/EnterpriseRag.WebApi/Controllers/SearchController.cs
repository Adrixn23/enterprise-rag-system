namespace EnterpriseRag.WebApi.Controllers;

using EnterpriseRag.Core.Application.Contracts.Search;
using EnterpriseRag.Core.Application.DTOs.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(List<SearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Search(
        [FromBody] SearchQueryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveTenantId(out var tenantId))
        {
            return Unauthorized();
        }

        var result = await _searchService.SearchAsync(request, tenantId, cancellationToken);

        if (!result.IsSuccess)
        {
            var firstError = result.Errors.FirstOrDefault();
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = firstError?.Code ?? "Search.Failed",
                Detail = firstError?.Description ?? "Ocurrió un error al procesar la búsqueda semántica."
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
