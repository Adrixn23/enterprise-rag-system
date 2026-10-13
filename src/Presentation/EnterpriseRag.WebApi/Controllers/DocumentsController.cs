namespace EnterpriseRag.WebApi.Controllers;

using EnterpriseRag.Core.Application.Contracts.Documents;
using EnterpriseRag.Core.Application.DTOs.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [ProducesResponseType(typeof(DocumentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Document.EmptyFile",
                Detail = "No file was uploaded or the uploaded file is empty."
            });
        }

        if (!TryResolveTenantId(out var tenantId))
        {
            return Unauthorized();
        }

        var request = new UploadDocumentRequestDto
        {
            File = file,
            TenantId = tenantId
        };

        var result = await _documentService.UploadDocumentAsync(request, cancellationToken);

        if (!result.IsSuccess)
        {
            var firstError = result.Errors.FirstOrDefault();
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = firstError?.Code ?? "Document.UploadFailed",
                Detail = firstError?.Description ?? "Failed to upload and process document."
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryResolveTenantId(out var tenantId))
        {
            return Unauthorized();
        }

        var result = await _documentService.GetByIdAsync(id, tenantId, cancellationToken);

        if (!result.IsSuccess)
        {
            var firstError = result.Errors.FirstOrDefault();
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = firstError?.Code ?? "Document.NotFound",
                Detail = firstError?.Description ?? "The requested document was not found."
            });
        }

        return Ok(result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DocumentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetByTenant(CancellationToken cancellationToken = default)
    {
        if (!TryResolveTenantId(out var tenantId))
        {
            return Unauthorized();
        }

        var result = await _documentService.GetByTenantAsync(tenantId, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/chunks")]
    [ProducesResponseType(typeof(IEnumerable<DocumentChunkResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetChunks(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryResolveTenantId(out var tenantId))
        {
            return Unauthorized();
        }

        var result = await _documentService.GetChunksByDocumentIdAsync(id, tenantId, cancellationToken);

        if (!result.IsSuccess)
        {
            var firstError = result.Errors.FirstOrDefault();
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = firstError?.Code ?? "Document.NotFound",
                Detail = firstError?.Description ?? "The chunks for the requested document were not found."
            });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryResolveTenantId(out var tenantId))
        {
            return Unauthorized();
        }

        var result = await _documentService.DeleteDocumentAsync(id, tenantId, cancellationToken);

        if (!result.IsSuccess)
        {
            var firstError = result.Errors.FirstOrDefault();
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = firstError?.Code ?? "Document.NotFound",
                Detail = firstError?.Description ?? "The requested document could not be found or deleted."
            });
        }

        return NoContent();
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
