namespace RagPipeline.BuildingBlocks.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class ConfigurationValidationExtensions
{
    public static IServiceCollection AddStartupConfigurationValidation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Hosting Options
        services.AddSingleton<IValidateOptions<HostingOptions>, HostingOptionsValidator>();
        services.AddOptions<HostingOptions>()
            .Bind(configuration.GetSection(HostingOptions.SectionName))
            .ValidateOnStart();

        // Database Options
        services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
        services.AddOptions<DatabaseOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                options.ConnectionString = config.GetConnectionString("Database")
                    ?? config[DatabaseOptions.ConfigurationKey];
            })
            .ValidateOnStart();

        // ReverseProxy Options
        services.AddSingleton<IValidateOptions<ReverseProxyOptions>, ReverseProxyOptionsValidator>();
        services.AddOptions<ReverseProxyOptions>()
            .Bind(configuration.GetSection(ReverseProxyOptions.SectionName))
            .ValidateOnStart();

        return services;
    }
}
