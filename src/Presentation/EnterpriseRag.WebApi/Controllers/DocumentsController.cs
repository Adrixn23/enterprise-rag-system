namespace EnterpriseRag.WebApi.Controllers;

using EnterpriseRag.Core.Application.Contracts.Documents;
using EnterpriseRag.Core.Application.DTOs.Common;
using EnterpriseRag.Core.Application.DTOs.Documents;
using EnterpriseRag.WebApi.Common;
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
    [ProducesResponseType(typeof(DocumentResponseDto), StatusCodes.Status202Accepted)]
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
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            OpenReadStream = file.OpenReadStream,
            TenantId = tenantId
        };

        var result = await _documentService.EnqueueDocumentAsync(request, cancellationToken);
        return result.ToActionResult(doc => AcceptedAtAction(nameof(GetById), new { id = doc.Id }, doc));
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
        return result.ToActionResult();
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponseDto<DocumentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetByTenant(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveTenantId(out var tenantId))
        {
            return Unauthorized();
        }

        var result = await _documentService.GetPagedByTenantAsync(tenantId, page, pageSize, cancellationToken);
        return result.ToActionResult();
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
        return result.ToActionResult();
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
