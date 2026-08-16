using FoundU.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Middleware;

/// <summary>
/// Single place every unhandled exception in the pipeline passes through. Translates
/// AppException subclasses into the specific HTTP status they represent, and anything else into
/// a generic 500 with no internal details leaked to the client. See /docs/api/conventions.md
/// "Error envelope" for the exact response shape every client should expect.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        var (statusCode, title) = exception switch
        {
            ValidationAppException => (StatusCodes.Status400BadRequest, "Validation failed"),
            UnauthorizedAppException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ForbiddenAppException => (StatusCodes.Status403Forbidden, "Forbidden"),
            NotFoundAppException => (StatusCodes.Status404NotFound, "Not found"),
            ConflictAppException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            // Full detail goes to the server log only - never to the client.
            _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);
        }
        else
        {
            _logger.LogWarning(exception, "Handled {ExceptionType}. TraceId: {TraceId}", exception.GetType().Name, traceId);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://httpstatuses.com/{statusCode}",
            Instance = httpContext.Request.Path,
            // In production, hide raw exception messages for 500s (could leak internals); AppException
            // subclasses (400-409) carry deliberately client-safe messages, so those are always shown.
            Detail = statusCode == StatusCodes.Status500InternalServerError && !_environment.IsDevelopment()
                ? "An unexpected error occurred. Please try again or contact support."
                : exception.Message
        };

        problemDetails.Extensions["traceId"] = traceId;

        if (exception is ValidationAppException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
