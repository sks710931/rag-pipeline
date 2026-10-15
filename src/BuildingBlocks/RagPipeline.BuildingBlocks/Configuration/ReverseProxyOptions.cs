namespace RagPipeline.BuildingBlocks.Configuration;

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    public bool Enabled { get; set; }

    public List<string> KnownProxies { get; set; } = new();

    public List<string> KnownNetworks { get; set; } = new();

    public List<string> AllowedHosts { get; set; } = new();

    public int ForwardLimit { get; set; } = 1;
}

public sealed class ReverseProxyOptionsValidator : IValidateOptions<ReverseProxyOptions>
{
    public ValidateOptionsResult Validate(string? name, ReverseProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        var proxiesCount = options.KnownProxies?.Count ?? 0;
        var networksCount = options.KnownNetworks?.Count ?? 0;

        if (proxiesCount == 0 && networksCount == 0)
        {
            failures.Add("ReverseProxy:KnownProxies or ReverseProxy:KnownNetworks must contain at least one entry when ReverseProxy:Enabled is true.");
        }

        if (options.KnownProxies != null)
        {
            for (var i = 0; i < options.KnownProxies.Count; i++)
            {
                var proxy = options.KnownProxies[i];
                if (string.IsNullOrWhiteSpace(proxy) || !IPAddress.TryParse(proxy, out _))
                {
                    failures.Add($"ReverseProxy:KnownProxies:{i} is not a valid IP address");
                }
            }
        }

        if (options.KnownNetworks != null)
        {
            for (var i = 0; i < options.KnownNetworks.Count; i++)
            {
                var network = options.KnownNetworks[i];
                if (!IsValidCidr(network))
                {
                    failures.Add($"ReverseProxy:KnownNetworks:{i} is not a valid CIDR network");
                }
            }
        }

        if (options.AllowedHosts != null)
        {
            for (var i = 0; i < options.AllowedHosts.Count; i++)
            {
                var host = options.AllowedHosts[i];
                if (!IsValidHost(host))
                {
                    failures.Add($"ReverseProxy:AllowedHosts:{i} is not a valid host name");
                }
            }
        }

        if (options.ForwardLimit < 1 || options.ForwardLimit > 5)
        {
            failures.Add("ReverseProxy:ForwardLimit must be between 1 and 5.");
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsValidCidr(string? cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr))
        {
            return false;
        }

        var parts = cidr.Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!IPAddress.TryParse(parts[0], out var ip))
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var prefixLength))
        {
            return false;
        }

        return ip.AddressFamily switch
        {
            AddressFamily.InterNetwork => prefixLength >= 0 && prefixLength <= 32,
            AddressFamily.InterNetworkV6 => prefixLength >= 0 && prefixLength <= 128,
            _ => false
        };
    }

    private static bool IsValidHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (host == "*")
        {
            return false;
        }

        if (host.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (host.Contains('/') || host.Contains('\\') || host.Contains(':') || host.Contains('?') || host.Contains('#'))
        {
            return false;
        }

        var domain = host;
        if (domain.StartsWith("*.", StringComparison.Ordinal))
        {
            domain = domain[2..];
        }

        if (string.IsNullOrEmpty(domain) || domain.Contains('*'))
        {
            return false;
        }

        var hostType = Uri.CheckHostName(domain);
        return hostType == UriHostNameType.Dns || hostType == UriHostNameType.IPv4;
    }
}
