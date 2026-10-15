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
using RagPipeline.BuildingBlocks.Persistence;
using RagPipeline.BuildingBlocks.Readiness;

public sealed record DeploymentAdminStatusResponse(string Status, DateTimeOffset Timestamp);
public sealed record DeploymentKeyRootResponse(string Status, string KeyRingVersion);

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

        var bootstrapGroup = endpoints.MapGroup("/api/v1/bootstrap");

        bootstrapGroup.MapGet("/status", async (
            HttpContext httpContext,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store, no-cache";

            var bootstrapService = serviceProvider.GetService<IBootstrapStateService>();
            if (bootstrapService == null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Service Unavailable",
                    detail: "DATABASE_UNAVAILABLE",
                    type: "https://tools.ietf.org/html/rfc9457");
            }

            var result = await bootstrapService.GetStatusAsync(cancellationToken);
            if (result.IsUnavailable)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Service Unavailable",
                    detail: result.ReasonCode ?? "DATABASE_UNAVAILABLE",
                    type: "https://tools.ietf.org/html/rfc9457");
            }

            var stateString = result.Kind switch
            {
                BootstrapStateKind.Uninitialized => "UNINITIALIZED",
                BootstrapStateKind.Bootstrapping => "BOOTSTRAPPING",
                BootstrapStateKind.Sealed => "SEALED",
                _ => "SEALED"
            };

            return Results.Ok(new BootstrapStatusResponse(stateString));
        })
        .WithName("GetBootstrapStatus")
        .WithTags("Bootstrap")
        .WithSummary("Get deployment bootstrap status")
        .Produces<BootstrapStatusResponse>(StatusCodes.Status200OK, "application/json")
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable, "application/problem+json")
        .AllowAnonymous();
    }
}
