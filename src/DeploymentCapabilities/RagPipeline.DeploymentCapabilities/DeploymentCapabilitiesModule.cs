namespace RagPipeline.DeploymentCapabilities;

using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RagPipeline.BuildingBlocks.Authorization;
using RagPipeline.BuildingBlocks.Modules;
using RagPipeline.BuildingBlocks.Readiness;

public sealed record DeploymentAdminStatusResponse(string Status, DateTimeOffset Timestamp);
public sealed record DeploymentKeyRootResponse(string Status, string KeyRingVersion);
public sealed record DeploymentBootstrapSealResponse(bool Sealed, DateTimeOffset SealedAt);

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

        healthGroup.MapGet("/live", () => Results.Ok(new LiveHealthResponse("live")))
            .WithName("GetLiveHealth")
            .WithTags("Health")
            .WithSummary("Liveness probe")
            .Produces<LiveHealthResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status500InternalServerError, "application/problem+json")
            .AllowAnonymous();

        healthGroup.MapGet("/ready", async (IServiceProvider sp, IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var checks = sp.GetServices<IReadinessCheck>();
            var report = await ReadinessAggregator.AggregateAsync(checks, configuration, cancellationToken);

            return report.IsReady
                ? Results.Ok(report)
                : Results.Json(report, statusCode: StatusCodes.Status503ServiceUnavailable, contentType: "application/json");
        })
        .WithName("GetReadyHealth")
        .WithTags("Health")
        .WithSummary("Readiness probe")
        .Produces<ReadinessReport>(StatusCodes.Status200OK, "application/json")
        .Produces<ReadinessReport>(StatusCodes.Status503ServiceUnavailable, "application/json")
        .ProducesProblem(StatusCodes.Status500InternalServerError, "application/problem+json")
        .AllowAnonymous();

        var adminGroup = endpoints.MapGroup("/api/v1/deployment/admin");

        adminGroup.MapGet("/status", () => Results.Ok(new DeploymentAdminStatusResponse("Operational", DateTimeOffset.UtcNow)))
            .WithName("GetDeploymentAdminStatus")
            .WithTags("DeploymentAdmin")
            .WithSummary("Get deployment administration status")
            .Produces<DeploymentAdminStatusResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status401Unauthorized, "application/problem+json")
            .ProducesProblem(StatusCodes.Status403Forbidden, "application/problem+json")
            .RequireAuthorization(Policies.DeploymentAdmin);

        adminGroup.MapGet("/keys/root", () => Results.Ok(new DeploymentKeyRootResponse("RootKeyAdminOperational", "v1")))
            .WithName("GetDeploymentKeyRoot")
            .WithTags("DeploymentAdmin")
            .WithSummary("Get deployment root key status")
            .Produces<DeploymentKeyRootResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status401Unauthorized, "application/problem+json")
            .ProducesProblem(StatusCodes.Status403Forbidden, "application/problem+json")
            .RequireAuthorization(Policies.KeyRootAndBootstrapAdmin);

        adminGroup.MapPost("/bootstrap/seal", () => Results.Ok(new DeploymentBootstrapSealResponse(true, DateTimeOffset.UtcNow)))
            .WithName("SealDeploymentBootstrap")
            .WithTags("DeploymentAdmin")
            .WithSummary("Seal deployment bootstrap")
            .Produces<DeploymentBootstrapSealResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status401Unauthorized, "application/problem+json")
            .ProducesProblem(StatusCodes.Status403Forbidden, "application/problem+json")
            .RequireAuthorization(Policies.KeyRootAndBootstrapAdmin);
    }
}
