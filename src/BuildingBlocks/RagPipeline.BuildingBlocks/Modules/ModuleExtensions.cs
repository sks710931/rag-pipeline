namespace RagPipeline.BuildingBlocks.Modules;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ModuleExtensions
{
    public static IServiceCollection AddModule<TModule>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TModule : class, IModule, new()
    {
        var module = new TModule();
        services.AddSingleton<IModule>(module);
        module.RegisterModule(services, configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var modules = endpoints.ServiceProvider.GetServices<IModule>();
        foreach (var module in modules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
