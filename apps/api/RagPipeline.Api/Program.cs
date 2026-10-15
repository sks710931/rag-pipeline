using Microsoft.Extensions.Options;
using RagPipeline.Api;
using RagPipeline.BuildingBlocks.Configuration;
using RagPipeline.BuildingBlocks.ErrorHandling;
using RagPipeline.BuildingBlocks.Hosting;
using RagPipeline.BuildingBlocks.Modules;
using RagPipeline.DeploymentCapabilities;
using RagPipeline.IdentityAccess;
using RagPipeline.TenantManagement;

public partial class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddHostingBaseline();

            var secretsDir = Environment.GetEnvironmentVariable("RAGPIPELINE_SECRETS_DIR");
            if (string.IsNullOrWhiteSpace(secretsDir))
            {
                secretsDir = "/run/secrets";
            }

            builder.Configuration.AddKeyPerFile(directoryPath: secretsDir, optional: true, reloadOnChange: false);

            // Startup configuration validation
            builder.Services.AddStartupConfigurationValidation(builder.Configuration);

            // RFC 9457 Problem Details contract
            builder.Services.AddProblemDetailsContract();

            // Module registration
            builder.Services.AddModule<IdentityAccessModule>(builder.Configuration);
            builder.Services.AddModule<TenantManagementModule>(builder.Configuration);
            builder.Services.AddModule<DeploymentCapabilitiesModule>(builder.Configuration);

            // OpenAPI document generation (build-time / test-time only; not served at runtime)
            builder.Services.AddRagPipelineOpenApi();

            var app = builder.Build();

            // Explicitly trigger ValidateOnStart options validation before host startup
            // to ensure sanitized diagnostics are written without runtime host stack traces
            _ = app.Services.GetRequiredService<IOptions<HostingOptions>>().Value;
            _ = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            _ = app.Services.GetRequiredService<IOptions<ReverseProxyOptions>>().Value;

            app.UseHostingBaseline();

            // Problem Details error handling pipeline
            app.UseProblemDetailsContract();

            // Map module endpoints
            app.MapModuleEndpoints();

            // Serve static assets and SPA fallback with strict /api exclusion
            app.UseSpaHosting();

            app.Run();
            return 0;
        }
        catch (OptionsValidationException ex)
        {
            var sanitized = ConfigurationSanitizer.Sanitize(ex);
            Console.Error.WriteLine(sanitized);
            return 1;
        }
    }
}
