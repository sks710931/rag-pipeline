namespace RagPipeline.BuildingBlocks.Tenancy;

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

public sealed class TenantContextEndpointFilter : IEndpointFilter
{
    private readonly ITenantContextResolver _resolver;

    public TenantContextEndpointFilter(ITenantContextResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await _resolver.ResolveAsync(context.HttpContext, context.HttpContext.RequestAborted);
        if (!result.IsSuccess)
        {
            return Results.Problem(
                statusCode: result.StatusCode,
                title: result.StatusCode == StatusCodes.Status404NotFound ? "Not Found" : "Unauthorized",
                detail: result.ErrorMessage,
                type: result.StatusCode == StatusCodes.Status404NotFound
                    ? "https://tools.ietf.org/html/rfc9110#section-15.5.5"
                    : "https://tools.ietf.org/html/rfc9110#section-15.5.2");
        }

        return await next(context);
    }
}
