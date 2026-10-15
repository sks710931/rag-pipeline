namespace RagPipeline.BuildingBlocks.Configuration;

using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class ReverseProxyExtensions
{
    public static WebApplication UseReverseProxyConfiguration(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.Services.GetRequiredService<IOptions<ReverseProxyOptions>>().Value;
        if (!options.Enabled)
        {
            return app;
        }

        var forwardedOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
            ForwardLimit = options.ForwardLimit
        };

        forwardedOptions.KnownProxies.Clear();
        if (options.KnownProxies != null)
        {
            foreach (var proxy in options.KnownProxies)
            {
                if (IPAddress.TryParse(proxy, out var ip))
                {
                    forwardedOptions.KnownProxies.Add(ip);
                }
            }
        }

        forwardedOptions.KnownIPNetworks.Clear();
        if (options.KnownNetworks != null)
        {
            foreach (var cidr in options.KnownNetworks)
            {
                if (string.IsNullOrWhiteSpace(cidr))
                {
                    continue;
                }

                var parts = cidr.Split('/');
                if (parts.Length == 2 &&
                    IPAddress.TryParse(parts[0], out var ip) &&
                    int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var prefixLength))
                {
                    forwardedOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(ip, prefixLength));
                }
            }
        }

        forwardedOptions.AllowedHosts.Clear();
        if (options.AllowedHosts != null)
        {
            foreach (var host in options.AllowedHosts)
            {
                if (!string.IsNullOrWhiteSpace(host))
                {
                    forwardedOptions.AllowedHosts.Add(host);
                }
            }
        }

        app.UseForwardedHeaders(forwardedOptions);

        return app;
    }
}
