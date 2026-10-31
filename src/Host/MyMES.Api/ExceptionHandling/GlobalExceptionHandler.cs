using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyMES.Domain.Common;

namespace MyMES.Api.ExceptionHandling;

/// <summary>
/// Turns anything that escapes an endpoint into a consistent <c>ProblemDetails</c> response.
/// </summary>
/// <remarks>
/// Expected failures never reach here: they travel as results and are mapped by the endpoint. So
/// what arrives is either a domain invariant that slipped past a precondition check, or a genuine
/// fault. The first is a 409 the caller can act on; the second is a 500 whose detail is withheld
/// outside Development, because exception text leaks schema, paths and library versions.
/// </remarks>
public sealed class GlobalExceptionHandler(
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // The caller going away is not a server fault and should not page anyone.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request {Path} was cancelled by the client.", httpContext.Request.Path);
            return true;
        }

        var isDomainFailure = exception is DomainException;
        var status = isDomainFailure
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status500InternalServerError;

        var problem = new ProblemDetails
        {
            Title = isDomainFailure ? "Conflict" : "Unexpected error",
            Detail = exception switch
            {
                DomainException domain => domain.Message,
                _ when environment.IsDevelopment() => exception.ToString(),
                _ => "An unexpected error occurred. Quote the trace id when reporting it."
            },
            Status = status
        };

        if (exception is DomainException domainException)
        {
            problem.Extensions["code"] = domainException.Code;
        }

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}.",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Domain rule violated on {Path}: {Message}",
                httpContext.Request.Path, exception.Message);
        }

        problem.Instance = httpContext.Request.Path;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);

        return true;
    }
}
