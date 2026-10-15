namespace RagPipeline.TenantManagement;

using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RagPipeline.BuildingBlocks.Modules;
using RagPipeline.BuildingBlocks.Tenancy;

public sealed class TenantManagementModule : IModule
{
    public string Name => "TenantManagement";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var tenantGroup = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .AddEndpointFilter<TenantContextEndpointFilter>();

        tenantGroup.MapGet("/profile", async (
            Guid tenantId,
            ITenantProfileStore profileStore,
            CancellationToken ct) =>
        {
            var profile = await profileStore.GetProfileAsync(tenantId, ct);
            if (profile == null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: "Tenant profile not found.",
                    type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
            }

            return Results.Ok(profile);
        });
    }
}
