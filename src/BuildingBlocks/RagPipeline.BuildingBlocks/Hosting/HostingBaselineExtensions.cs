namespace RagPipeline.BuildingBlocks.Hosting;

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using RagPipeline.BuildingBlocks.Configuration;
using RagPipeline.BuildingBlocks.Http;
using RagPipeline.BuildingBlocks.Observability;

using RagPipeline.BuildingBlocks.Tenancy;

public static class HostingBaselineExtensions
{
    public static IServiceCollection AddHostingBaseline(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
        services.AddScoped<ITenantContextResolver, TenantContextResolver>();
        services.AddScoped<TenantContextEndpointFilter>();

        services.AddLogging(builder =>
        {
            builder.AddConsoleFormatter<RedactingJsonConsoleFormatter, ConsoleFormatterOptions>()
                   .AddConsole(options => options.FormatterName = RedactingJsonConsoleFormatter.FormatName);

            builder.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
            builder.AddFilter("Microsoft.AspNetCore.*", LogLevel.Warning);
        });

        return services;
    }

    public static WebApplication UseHostingBaseline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseReverseProxyConfiguration();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ApiSecurityHeadersMiddleware>();

        return app;
    }
}
