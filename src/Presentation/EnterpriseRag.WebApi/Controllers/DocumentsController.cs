namespace EnterpriseRag.WebApi.Controllers;

using System.Security.Claims;
using EnterpriseRag.Core.Application.Contracts.Documents;
using EnterpriseRag.Core.Application.DTOs.Documents;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromForm] string? tenantId = null,
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

        var effectiveTenantId = !string.IsNullOrWhiteSpace(tenantId)
            ? tenantId
            : ResolveTenantId();

        var request = new UploadDocumentRequestDto
        {
            File = file,
            TenantId = effectiveTenantId
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
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _documentService.GetByIdAsync(id, ResolveTenantId(), cancellationToken);

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
    public async Task<IActionResult> GetByTenant(CancellationToken cancellationToken = default)
    {
        var result = await _documentService.GetByTenantAsync(ResolveTenantId(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/chunks")]
    [ProducesResponseType(typeof(IEnumerable<DocumentChunkResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChunks(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _documentService.GetChunksByDocumentIdAsync(id, ResolveTenantId(), cancellationToken);

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
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _documentService.DeleteDocumentAsync(id, ResolveTenantId(), cancellationToken);

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

    private string ResolveTenantId()
    {
        return User.FindFirst("tenant_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Request.Headers["X-Tenant-Id"].FirstOrDefault()
            ?? "default-tenant";
    }
}
