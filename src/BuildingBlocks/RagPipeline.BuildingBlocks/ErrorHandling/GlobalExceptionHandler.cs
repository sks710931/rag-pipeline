namespace RagPipeline.BuildingBlocks.ErrorHandling;

using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception occurred during request execution.");

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = ProblemDetailsExtensions.ProblemJsonMediaType;

        var traceId = Activity.Current is { TraceId: var tid } && tid != default
            ? tid.ToString()
            : httpContext.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Title = "An error occurred while processing your request.",
            Status = StatusCodes.Status500InternalServerError,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = traceId;

        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: ProblemDetailsExtensions.ProblemJsonMediaType,
            cancellationToken: cancellationToken);

        return true;
    }
}
