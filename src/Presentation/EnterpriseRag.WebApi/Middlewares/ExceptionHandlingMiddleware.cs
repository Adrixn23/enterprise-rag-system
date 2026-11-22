namespace EnterpriseRag.WebApi.Middlewares;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

public class ExceptionHandlingMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var incomingCorrelationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        var traceId = !string.IsNullOrWhiteSpace(incomingCorrelationId) && incomingCorrelationId.Length <= 128
            ? incomingCorrelationId.Trim()
            : Activity.Current?.Id ?? context.TraceIdentifier;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = traceId;
            return Task.CompletedTask;
        });

        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId });

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("La solicitud HTTP fue cancelada por el cliente. TraceId: {TraceId}", traceId);

            // No escribir en la respuesta si el cliente ya cerró el socket o si las cabeceras ya se enviaron
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499; // Client Closed Request
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción no controlada capturada por el middleware global. TraceId: {TraceId}", traceId);

            if (!context.Response.HasStarted)
            {
                await HandleExceptionAsync(context, ex, traceId);
            }
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, string traceId)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Server.InternalError",
            Detail = "Ocurrió un error inesperado en el servidor al procesar la solicitud.",
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = traceId;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, jsonOptions));
    }
}
