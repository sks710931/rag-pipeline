namespace RagPipeline.BuildingBlocks.Tenancy;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RagPipeline.BuildingBlocks.ErrorHandling;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        ITenantContextResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(resolver);

        var path = httpContext.Request.Path.Value;
        if (path != null && path.StartsWith("/api/v1/tenants/", StringComparison.OrdinalIgnoreCase))
        {
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var result = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
                if (!result.IsSuccess)
                {
                    httpContext.Response.StatusCode = result.StatusCode;
                    httpContext.Response.ContentType = ProblemDetailsExtensions.ProblemJsonMediaType;

                    var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
                    var problem = new ProblemDetails
                    {
                        Status = result.StatusCode,
                        Title = result.StatusCode == StatusCodes.Status404NotFound ? "Not Found" : "Unauthorized",
                        Detail = result.ErrorMessage,
                        Type = result.StatusCode == StatusCodes.Status404NotFound
                            ? "https://tools.ietf.org/html/rfc9110#section-15.5.5"
                            : "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                        Instance = httpContext.Request.Path
                    };
                    problem.Extensions["traceId"] = traceId;

                    await httpContext.Response.WriteAsJsonAsync(
                        problem,
                        options: null,
                        contentType: ProblemDetailsExtensions.ProblemJsonMediaType);
                    return;
                }
            }
        }

        await _next(httpContext);
    }
}
