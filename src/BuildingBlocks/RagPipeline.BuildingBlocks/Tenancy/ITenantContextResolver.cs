namespace RagPipeline.BuildingBlocks.Tenancy;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

public interface ITenantContextResolver
{
    Task<TenantResolutionResult> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
