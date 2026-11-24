using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Common.Exceptions;
using Starter.Domain.Common;

namespace Starter.Api.ErrorHandling;

/// <summary>
/// Translates known exceptions into RFC 9457 problem responses. Anything unrecognised becomes a
/// generic 500 whose details stay in the logs, correlated by the trace id in the response.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request was cancelled by the client");
            return true;
        }

        var (status, title, code) = exception switch
        {
            DomainException e => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", e.Code),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", "not_found"),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Forbidden", "forbidden"),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Code),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Concurrency conflict", "concurrency_conflict"),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Authentication failed", "authentication_failed"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "internal_error"),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            // Messages of handled exceptions are written for end users; internal errors never leak details.
            Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
            Instance = httpContext.Request.Path,
            Extensions = { ["code"] = code },
        };

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
