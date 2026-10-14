namespace RagPipeline.IdentityAccess;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RagPipeline.BuildingBlocks.Modules;

public sealed class IdentityAccessModule : IModule
{
    public string Name => "IdentityAccess";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        // Placeholder module registration for S0-001
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Placeholder endpoint mapping for S0-001
    }
}
