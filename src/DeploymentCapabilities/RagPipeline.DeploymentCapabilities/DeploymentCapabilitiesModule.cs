namespace RagPipeline.DeploymentCapabilities;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RagPipeline.BuildingBlocks.Modules;

public sealed class DeploymentCapabilitiesModule : IModule
{
    public string Name => "DeploymentCapabilities";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        // Minimal registration for S0-001
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var healthGroup = endpoints.MapGroup("/api/v1/system/health");

        healthGroup.MapGet("/live", () => Results.Ok(new { status = "live" }))
            .AllowAnonymous();

        healthGroup.MapGet("/ready", (IConfiguration configuration) =>
        {
            var dbConfigured = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Database")
                ?? configuration["ConnectionStrings:Database"]);

            var qdrantConfigured = IsDependencyConfigured(configuration, "Qdrant", "Qdrant:Endpoint", "Qdrant:Url", "ConnectionStrings:Qdrant");
            var aiConfigured = IsDependencyConfigured(configuration, "Ai", "Ai:ApiKey", "Ai:Endpoint", "OpenAi:ApiKey");
            var azureConfigured = IsDependencyConfigured(configuration, "Azure", "Azure:ApiKey", "Azure:Endpoint", "Azure:OcrEndpoint");

            return Results.Ok(new
            {
                status = "ready",
                dependencies = new
                {
                    database = dbConfigured ? "configured" : "not_configured",
                    qdrant = qdrantConfigured ? "configured" : "not_configured",
                    ai = aiConfigured ? "configured" : "not_configured",
                    azure = azureConfigured ? "configured" : "not_configured"
                }
            });
        }).AllowAnonymous();
    }

    private static bool IsDependencyConfigured(IConfiguration configuration, string sectionName, params string[] explicitKeys)
    {
        foreach (var key in explicitKeys)
        {
            if (!string.IsNullOrWhiteSpace(configuration[key]))
            {
                return true;
            }
        }

        var section = configuration.GetSection(sectionName);
        if (section.Exists() && section.GetChildren().Any(c => !string.IsNullOrWhiteSpace(c.Value)))
        {
            return true;
        }

        return false;
    }
}
