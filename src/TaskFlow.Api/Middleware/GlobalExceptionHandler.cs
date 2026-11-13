using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Api.Middleware;

/// <summary>
/// Translates exceptions into RFC 9457 ProblemDetails responses. Expected business
/// errors are logged at Information/Warning; only unexpected failures are logged as
/// errors, and their internals are never leaked to the client.
/// </summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499; // client closed request; nothing to report
            return true;
        }

        var (status, title, detail, code) = exception switch
        {
            RequestValidationException => (StatusCodes.Status400BadRequest, "Validation failed", exception.Message, "validation"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message, "not_found"),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Forbidden", exception.Message, "forbidden"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", exception.Message, "unauthorized"),
            ConflictException c => (StatusCodes.Status409Conflict, "Conflict", c.Message, c.Code),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict",
                "This record was changed by someone else after you loaded it. Reload to see the latest version and try again.",
                "concurrency"),
            DomainException { Code: "task.cancel_forbidden" } d => (StatusCodes.Status403Forbidden, "Forbidden", d.Message, d.Code),
            DomainException d => (StatusCodes.Status409Conflict, "Business rule violation", d.Message, d.Code),
            BadHttpRequestException b => (b.StatusCode, "Bad request", b.Message, "bad_request"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error",
                "An unexpected error occurred. Use the correlation id when reporting this issue.", "server_error"),
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogInformation("Request failed with {StatusCode} ({Code}): {Message}", status, code, exception.Message);

        ProblemDetails problem = exception is RequestValidationException validation
            ? new ValidationProblemDetails(validation.Errors) { Title = title, Status = status, Detail = detail }
            : new ProblemDetails { Title = title, Status = status, Detail = detail };
        problem.Extensions["code"] = code;

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
