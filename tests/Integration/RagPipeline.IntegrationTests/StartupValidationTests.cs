namespace RagPipeline.IntegrationTests;

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
}
