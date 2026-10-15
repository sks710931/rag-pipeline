namespace RagPipeline.IntegrationTests;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

public class StartupValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StartupValidationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Startup_WhenDatabaseConfigInvalid_FailsAtStartupWithSanitizedDiagnostic()
    {
        var originalError = Console.Error;
        using var stringWriter = new StringWriter();
        Console.SetError(stringWriter);

        try
        {
            using var customFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Database"] = "Password=SECRETVALUE123;garbage"
                    });
                });
            });

            Assert.ThrowsAny<Exception>(() =>
            {
                using var client = customFactory.CreateClient();
            });

            var stderr = stringWriter.ToString();
            Assert.Contains("ConnectionStrings:Database", stderr);
            Assert.DoesNotContain("SECRETVALUE123", stderr);
            Assert.DoesNotContain("garbage", stderr);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Startup_WhenDatabaseConfigMissing_FailsAtStartupWithSanitizedDiagnostic()
    {
        var originalError = Console.Error;
        using var stringWriter = new StringWriter();
        Console.SetError(stringWriter);

        try
        {
            using var customFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Database"] = ""
                    });
                });
            });

            Assert.ThrowsAny<Exception>(() =>
            {
                using var client = customFactory.CreateClient();
            });

            var stderr = stringWriter.ToString();
            Assert.Contains("ConnectionStrings:Database", stderr);
            Assert.Contains("required", stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void ProgramMain_WhenDatabaseConfigInvalid_ExitsNonZeroAndOutputsSanitizedStderr()
    {
        var originalError = Console.Error;
        using var stringWriter = new StringWriter();
        Console.SetError(stringWriter);

        try
        {
            var exitCode = Program.Main(["--ConnectionStrings:Database=Password=SECRETVALUE123;garbage"]);

            Assert.Equal(1, exitCode);

            var stderr = stringWriter.ToString();
            Assert.Contains("ConnectionStrings:Database", stderr);
            Assert.DoesNotContain("SECRETVALUE123", stderr);
            Assert.DoesNotContain("garbage", stderr);
            Assert.DoesNotContain("   at ", stderr);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void ProgramMain_WhenDatabaseConfigMissing_ExitsNonZeroAndOutputsSanitizedStderr()
    {
        var originalError = Console.Error;
        using var stringWriter = new StringWriter();
        Console.SetError(stringWriter);

        try
        {
            var exitCode = Program.Main(["--ConnectionStrings:Database="]);

            Assert.Equal(1, exitCode);

            var stderr = stringWriter.ToString();
            Assert.Contains("ConnectionStrings:Database", stderr);
            Assert.Contains("required", stderr, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("   at ", stderr);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Startup_WhenReverseProxyEnabledWithNoProxies_FailsAtStartupWithSanitizedDiagnostic()
    {
        var originalError = Console.Error;
        using var stringWriter = new StringWriter();
        Console.SetError(stringWriter);

        try
        {
            using var customFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ReverseProxy:Enabled"] = "true",
                        ["ConnectionStrings:Database"] = "Host=localhost;Database=ragpipeline;Username=ragpipeline"
                    });
                });
            });

            Assert.ThrowsAny<Exception>(() =>
            {
                using var client = customFactory.CreateClient();
            });

            var stderr = stringWriter.ToString();
            Assert.Contains("ReverseProxy:KnownProxies", stderr);
            Assert.DoesNotContain("   at ", stderr);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task Startup_WhenReverseProxyDisabled_Succeeds()
    {
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ReverseProxy:Enabled"] = "false",
                    ["ConnectionStrings:Database"] = "Host=localhost;Database=ragpipeline;Username=ragpipeline"
                });
            });
        });

        using var client = customFactory.CreateClient();
        var response = await client.GetAsync("/api/v1/system/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Startup_WhenReverseProxyEnabledWithLoopback_SucceedsAndHealthLiveReturns200()
    {
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ReverseProxy:Enabled"] = "true",
                    ["ReverseProxy:KnownProxies:0"] = "127.0.0.1",
                    ["ReverseProxy:AllowedHosts:0"] = "localhost",
                    ["ConnectionStrings:Database"] = "Host=localhost;Database=ragpipeline;Username=ragpipeline"
                });
            });
        });

        using var client = customFactory.CreateClient();
        var response = await client.GetAsync("/api/v1/system/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Startup_WhenCanaryInvalidReverseProxyConfig_CanaryValueNeverAppearsInStartupFailureOutput()
    {
        var canaryProxy = "CANARY-secret-proxy-value";
        var originalError = Console.Error;
        using var stringWriter = new StringWriter();
        Console.SetError(stringWriter);

        try
        {
            using var customFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ReverseProxy:Enabled"] = "true",
                        ["ReverseProxy:KnownProxies:0"] = canaryProxy,
                        ["ConnectionStrings:Database"] = "Host=localhost;Database=ragpipeline;Username=ragpipeline"
                    });
                });
            });

            Assert.ThrowsAny<Exception>(() =>
            {
                using var client = customFactory.CreateClient();
            });

            var stderr = stringWriter.ToString();
            Assert.Contains("ReverseProxy:KnownProxies:0", stderr);
            Assert.DoesNotContain(canaryProxy, stderr);
            Assert.DoesNotContain("   at ", stderr);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Startup_WhenCanaryDatabaseConnectionStringInvalidShape_CanaryValueNeverAppearsInStartupFailureOutput()
    {
        var canaryConnectionString = "CANARY-db-conn-string-invalid-shape-secret-token";
        var originalError = Console.Error;
        using var stringWriter = new StringWriter();
        Console.SetError(stringWriter);

        try
        {
            using var customFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Database"] = canaryConnectionString,
                        ["ReverseProxy:Enabled"] = "false"
                    });
                });
            });

            Assert.ThrowsAny<Exception>(() =>
            {
                using var client = customFactory.CreateClient();
            });

            var stderr = stringWriter.ToString();
            Assert.Contains("ConnectionStrings:Database", stderr);
            Assert.DoesNotContain(canaryConnectionString, stderr);
            Assert.DoesNotContain("   at ", stderr);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void ProgramMain_WhenReverseProxyEnabledWithNoProxies_ExitsNonZeroAndOutputsSanitizedStderr()
    {
        var originalError = Console.Error;
        using var stringWriter = new StringWriter();
        Console.SetError(stringWriter);

        try
        {
            var exitCode = Program.Main([
                "--ReverseProxy:Enabled=true",
                "--ConnectionStrings:Database=Host=localhost;Database=ragpipeline;Username=ragpipeline"
            ]);

            Assert.Equal(1, exitCode);

            var stderr = stringWriter.ToString();
            Assert.Contains("ReverseProxy:KnownProxies", stderr);
            Assert.DoesNotContain("   at ", stderr);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void ProgramMain_WhenCanaryInvalidReverseProxyConfig_ExitsNonZeroWithoutExposingCanary()
    {
        var canary = "CANARY-program-proxy-secret";
        var originalError = Console.Error;
        using var stringWriter = new StringWriter();
        Console.SetError(stringWriter);

        try
        {
            var exitCode = Program.Main([
                "--ReverseProxy:Enabled=true",
                $"--ReverseProxy:KnownProxies:0={canary}",
                "--ConnectionStrings:Database=Host=localhost;Database=ragpipeline;Username=ragpipeline"
            ]);

            Assert.Equal(1, exitCode);

            var stderr = stringWriter.ToString();
            Assert.Contains("ReverseProxy:KnownProxies:0", stderr);
            Assert.DoesNotContain(canary, stderr);
            Assert.DoesNotContain("   at ", stderr);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}
