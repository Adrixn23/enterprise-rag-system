namespace EnterpriseRag.WebApi.Controllers;

using EnterpriseRag.Core.Application.Contracts.Search;
using EnterpriseRag.Core.Application.DTOs.Search;
using EnterpriseRag.WebApi.Common;
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
        return result.ToActionResult();
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
