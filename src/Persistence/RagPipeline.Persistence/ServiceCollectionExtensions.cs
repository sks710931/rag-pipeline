namespace RagPipeline.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RagPipeline.BuildingBlocks.Configuration;
using RagPipeline.BuildingBlocks.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRagPipelinePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? configuration[DatabaseOptions.ConfigurationKey]
            ?? Environment.GetEnvironmentVariable("RAGPIPELINE_RUNTIME_CONNECTION");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<RagPipelineDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                });
            });

            services.AddScoped<IBootstrapStateReader, BootstrapStateReader>();
        }

        return services;
    }
}
