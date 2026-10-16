namespace RagPipeline.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RagPipeline.BuildingBlocks.Authorization;
using RagPipeline.BuildingBlocks.Bootstrap;
using RagPipeline.BuildingBlocks.Configuration;
using RagPipeline.BuildingBlocks.Persistence;
using RagPipeline.BuildingBlocks.Tenancy;
using RagPipeline.Persistence.Authorization;
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

        var maintenanceConnectionString = configuration.GetConnectionString("Maintenance")
            ?? configuration.GetConnectionString("Migrator")
            ?? configuration["ConnectionStrings:Maintenance"]
            ?? configuration["ConnectionStrings:Migrator"]
            ?? Environment.GetEnvironmentVariable("RAGPIPELINE_MAINTENANCE_CONNECTION")
            ?? Environment.GetEnvironmentVariable("RAGPIPELINE_MIGRATOR_CONNECTION");

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
            services.AddScoped<IBootstrapStateService, BootstrapStateService>();
            services.AddSingleton<IRandomNumberProvider, DefaultRandomNumberProvider>();
            services.AddSingleton<IConstantTimeComparator, DefaultConstantTimeComparator>();
            services.AddSingleton<IProofAttemptLimiter, ProofAttemptLimiter>();
            services.AddScoped<IBootstrapProofVerifier, BootstrapProofVerifier>();
            services.AddScoped<IBootstrapProofService, RagPipeline.Persistence.Bootstrap.BootstrapProofService>();
            services.AddScoped<IDeploymentOwnerLookup, EfDeploymentOwnerLookup>();
            services.AddScoped<ITenantMembershipLookup, EfTenantMembershipLookup>();
            services.AddScoped<ITenantProfileStore, EfTenantProfileStore>();
            services.AddScoped<ITenantTransactionCoordinator, EfTenantTransactionCoordinator>();
            services.AddScoped<RagPipeline.BuildingBlocks.Audit.IAuditEventStore>(sp =>
                new RagPipeline.Persistence.Audit.EfAuditEventStore(
                    sp.GetRequiredService<RagPipelineDbContext>(),
                    sp.GetRequiredService<ITenantTransactionCoordinator>(),
                    maintenanceConnectionString));
            services.AddScoped<RagPipeline.BuildingBlocks.Audit.IAuditRetentionService>(sp =>
                (RagPipeline.Persistence.Audit.EfAuditEventStore)sp.GetRequiredService<RagPipeline.BuildingBlocks.Audit.IAuditEventStore>());
            services.AddScoped<RagPipeline.BuildingBlocks.Session.ISessionRecordStore, RagPipeline.Persistence.Session.EfSessionRecordStore>();
        }

        return services;
    }
}

