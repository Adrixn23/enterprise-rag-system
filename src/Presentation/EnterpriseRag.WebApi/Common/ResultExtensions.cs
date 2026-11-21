namespace EnterpriseRag.WebApi.Common;

using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return new NoContentResult();
        }

        return MapErrorsToActionResult(result.Errors);
    }

    public static IActionResult ToActionResult<T>(this Result<T> result, Func<T, IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess != null ? onSuccess(result.Value!) : new OkObjectResult(result.Value);
        }

        return MapErrorsToActionResult(result.Errors);
    }

    private static IActionResult MapErrorsToActionResult(IEnumerable<Error> errors)
    {
        var firstError = errors.FirstOrDefault();
        var code = firstError?.Code ?? "Server.Error";
        var description = firstError?.Description ?? "Ocurrió un error en la operación solicitada.";

        // Mapeo semántico estricto de códigos de error a códigos HTTP
        var statusCode = code switch
        {
            var c when c.EndsWith(".NotFound", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status404NotFound,
            var c when c.EndsWith(".AccountLocked", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status423Locked,
            var c when c.EndsWith(".InvalidCredentials", StringComparison.OrdinalIgnoreCase) || c.EndsWith(".Unauthorized", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status401Unauthorized,
            var c when c.EndsWith(".Forbidden", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
            var c when c.EndsWith(".EmailAlreadyExists", StringComparison.OrdinalIgnoreCase) || c.EndsWith(".Conflict", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status409Conflict,
            var c when c.EndsWith(".PersistenceFailed", StringComparison.OrdinalIgnoreCase) 
                    || c.EndsWith(".Unexpected", StringComparison.OrdinalIgnoreCase)
                    || c.EndsWith(".InconsistentUserState", StringComparison.OrdinalIgnoreCase)
                    || c.StartsWith("Server.", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status500InternalServerError,
            var c when c.EndsWith(".ServiceUnavailable", StringComparison.OrdinalIgnoreCase) 
                    || c.EndsWith(".ExternalServiceError", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = code,
            Detail = description
        };

        return statusCode switch
        {
            StatusCodes.Status404NotFound => new NotFoundObjectResult(problemDetails),
            StatusCodes.Status401Unauthorized => new UnauthorizedObjectResult(problemDetails),
            StatusCodes.Status403Forbidden => new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status403Forbidden },
            StatusCodes.Status409Conflict => new ConflictObjectResult(problemDetails),
            StatusCodes.Status423Locked => new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status423Locked },
            StatusCodes.Status500InternalServerError => new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status500InternalServerError },
            _ => new BadRequestObjectResult(problemDetails)
        };
    }
}
