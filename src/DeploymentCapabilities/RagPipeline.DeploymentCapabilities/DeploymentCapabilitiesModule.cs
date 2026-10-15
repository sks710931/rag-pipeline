namespace RagPipeline.DeploymentCapabilities;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RagPipeline.BuildingBlocks.Modules;
using RagPipeline.BuildingBlocks.Readiness;

public sealed class DeploymentCapabilitiesModule : IModule
{
    public string Name => "DeploymentCapabilities";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReadinessCheck, DatabaseReadinessCheck>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var healthGroup = endpoints.MapGroup("/api/v1/system/health");

        healthGroup.MapGet("/live", () => Results.Ok(new { status = "live" }))
            .AllowAnonymous();

        healthGroup.MapGet("/ready", async (IServiceProvider sp, IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var checks = sp.GetServices<IReadinessCheck>();
            var report = await ReadinessAggregator.AggregateAsync(checks, configuration, cancellationToken);

            return report.IsReady
                ? Results.Ok(report)
                : Results.Json(report, statusCode: StatusCodes.Status503ServiceUnavailable, contentType: "application/json");
        }).AllowAnonymous();
    }
}
