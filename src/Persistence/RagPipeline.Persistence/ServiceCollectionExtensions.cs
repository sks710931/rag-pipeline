namespace RagPipeline.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RagPipeline.BuildingBlocks.Configuration;
using RagPipeline.BuildingBlocks.Persistence;
using RagPipeline.BuildingBlocks.Tenancy;
using RagPipeline.Persistence.Interceptors;
using RagPipeline.Persistence.Tenancy;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRagPipelinePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? configuration[DatabaseOptions.ConfigurationKey]
            ?? Environment.GetEnvironmentVariable("RAGPIPELINE_RUNTIME_CONNECTION");

        services.AddSingleton<TenantSecurityCommandInterceptor>();
        services.AddSingleton<PooledConnectionResetInterceptor>();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<RagPipelineDbContext>((sp, options) =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                });
                options.AddInterceptors(
                    sp.GetRequiredService<TenantSecurityCommandInterceptor>(),
                    sp.GetRequiredService<PooledConnectionResetInterceptor>());
            });

            services.AddScoped<IBootstrapStateReader, BootstrapStateReader>();
            services.AddScoped<ITenantMembershipLookup, EfTenantMembershipLookup>();
            services.AddScoped<ITenantProfileStore, EfTenantProfileStore>();
            services.AddScoped<ITenantTransactionCoordinator, EfTenantTransactionCoordinator>();
        }

        return services;
    }
}

