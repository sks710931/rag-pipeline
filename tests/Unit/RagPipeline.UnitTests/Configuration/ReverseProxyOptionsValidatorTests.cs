namespace RagPipeline.UnitTests.Configuration;

using RagPipeline.BuildingBlocks.Configuration;
using Xunit;

public class ReverseProxyOptionsValidatorTests
{
    private const string CanarySecretHost = "CANARY-secret-host";
    private readonly ReverseProxyOptionsValidator _validator = new();

    [Fact]
    public void Validate_WhenDisabled_SucceedsEvenWithEmptyConfiguration()
    {
        var options = new ReverseProxyOptions
        {
            Enabled = false,
            KnownProxies = new List<string>(),
            KnownNetworks = new List<string>(),
            AllowedHosts = new List<string>()
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenDisabled_SucceedsEvenWithInvalidValues()
    {
        var options = new ReverseProxyOptions
        {
            Enabled = false,
            KnownProxies = new List<string> { CanarySecretHost },
            KnownNetworks = new List<string> { "invalid-cidr" },
            AllowedHosts = new List<string> { "http://invalid" },
            ForwardLimit = 99
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenEnabledAndValidWithKnownProxies_Succeeds()
    {
        var options = new ReverseProxyOptions
        {
            Enabled = true,
            KnownProxies = new List<string> { "127.0.0.1", "10.0.0.1", "::1" },
            AllowedHosts = new List<string> { "example.com", "*.example.com", "localhost" },
            ForwardLimit = 2
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenEnabledAndValidWithKnownNetworks_Succeeds()
    {
        var options = new ReverseProxyOptions
        {
            Enabled = true,
            KnownNetworks = new List<string> { "10.0.0.0/8", "192.168.1.0/24", "::1/128", "fe80::/10" },
            ForwardLimit = 1
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Validate_WhenForwardLimitBetween1And5_Succeeds(int forwardLimit)
    {
        var options = new ReverseProxyOptions
        {
            Enabled = true,
            KnownProxies = new List<string> { "127.0.0.1" },
            ForwardLimit = forwardLimit
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenEnabledAndNoProxiesOrNetworksConfigured_FailsWithoutExposingSecrets()
    {
        var options = new ReverseProxyOptions
        {
            Enabled = true,
            KnownProxies = new List<string>(),
            KnownNetworks = new List<string>()
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ReverseProxy:KnownProxies", result.FailureMessage);
        Assert.Contains("ReverseProxy:KnownNetworks", result.FailureMessage);
    }

    [Theory]
    [InlineData(CanarySecretHost)]
    [InlineData("999.999.999.999")]
    [InlineData("not-an-ip-address")]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenKnownProxiesContainsInvalidIP_FailsAndNeverExposesValue(string invalidIp)
    {
        var options = new ReverseProxyOptions
        {
            Enabled = true,
            KnownProxies = new List<string> { "127.0.0.1", invalidIp }
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ReverseProxy:KnownProxies:1", result.FailureMessage);
        Assert.Contains("is not a valid IP address", result.FailureMessage);
        Assert.DoesNotContain(CanarySecretHost, result.FailureMessage);
        if (!string.IsNullOrWhiteSpace(invalidIp))
        {
            Assert.DoesNotContain(invalidIp, result.FailureMessage);
        }
    }

    [Theory]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.0/8/8")]
    [InlineData(CanarySecretHost + "/8")]
    [InlineData("10.0.0.0/abc")]
    [InlineData("10.0.0.0/-1")]
    [InlineData("10.0.0.0/33")]
    [InlineData("::1/129")]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenKnownNetworksContainsInvalidCidr_FailsAndNeverExposesValue(string invalidCidr)
    {
        var options = new ReverseProxyOptions
        {
            Enabled = true,
            KnownNetworks = new List<string> { invalidCidr }
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ReverseProxy:KnownNetworks:0", result.FailureMessage);
        Assert.Contains("is not a valid CIDR network", result.FailureMessage);
        Assert.DoesNotContain(CanarySecretHost, result.FailureMessage);
        if (!string.IsNullOrWhiteSpace(invalidCidr))
        {
            Assert.DoesNotContain(invalidCidr, result.FailureMessage);
        }
    }

    [Theory]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://" + CanarySecretHost)]
    [InlineData("https://" + CanarySecretHost)]
    [InlineData(CanarySecretHost + ":8080")]
    [InlineData(CanarySecretHost + "/path")]
    [InlineData("canary host with space")]
    [InlineData("*" + CanarySecretHost)]
    [InlineData("foo.*.com")]
    [InlineData("*.")]
    public void Validate_WhenAllowedHostsContainsInvalidHost_FailsAndNeverExposesValue(string invalidHost)
    {
        var options = new ReverseProxyOptions
        {
            Enabled = true,
            KnownProxies = new List<string> { "127.0.0.1" },
            AllowedHosts = new List<string> { invalidHost }
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ReverseProxy:AllowedHosts:0", result.FailureMessage);
        Assert.Contains("is not a valid host name", result.FailureMessage);
        Assert.DoesNotContain(CanarySecretHost, result.FailureMessage);
        if (!string.IsNullOrWhiteSpace(invalidHost) && invalidHost != "*")
        {
            Assert.DoesNotContain(invalidHost, result.FailureMessage);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(10)]
    public void Validate_WhenForwardLimitOutOfRange_FailsReportingKey(int invalidLimit)
    {
        var options = new ReverseProxyOptions
        {
            Enabled = true,
            KnownProxies = new List<string> { "127.0.0.1" },
            ForwardLimit = invalidLimit
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ReverseProxy:ForwardLimit", result.FailureMessage);
        Assert.Contains("between 1 and 5", result.FailureMessage);
    }
}
