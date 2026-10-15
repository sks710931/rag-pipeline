namespace RagPipeline.IntegrationTests;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RagPipeline.BuildingBlocks.ErrorHandling;
using Xunit;

public class SpaHostingTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string? _tempSpaDir;

    public SpaHostingTests(WebApplicationFactory<Program> factory)
    {
        var repoRoot = FindRepoRoot();
        var defaultDistIndex = Path.Combine(repoRoot, "apps", "web", "dist", "index.html");

        if (!File.Exists(defaultDistIndex))
        {
            // If apps/web has not been built yet, create a temporary SPA directory with index.html
            // so SPA hosting and fallback tests can run without hard failure.
            _tempSpaDir = Path.Combine(Path.GetTempPath(), "rag-spa-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempSpaDir);
            File.WriteAllText(
                Path.Combine(_tempSpaDir, "index.html"),
                "<!doctype html><html lang=\"en\"><head><title>Pixel RAG Test SPA</title></head><body><div id=\"root\"></div></body></html>");

            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Spa:Root"] = _tempSpaDir
                    });
                });
            });
        }
        else
        {
            _factory = factory;
        }
    }

    [Fact]
    public async Task Root_ServesIndexHtml_With200AndTextHtml()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("<html", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"root\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeepClientRoute_FallsBackToIndexHtml_With200AndTextHtml()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/app/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("<html", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"root\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/v1/system/unknown-route-that-does-not-exist")]
    [InlineData("/api/v1/nonexistent")]
    [InlineData("/api/unknown")]
    [InlineData("/api")]
    public async Task UnknownApiRoute_Returns404ProblemDetails_NeverIndexHtml(string apiPath)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(apiPath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemDetailsExtensions.ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();

        // Must never leak HTML markup in API error responses
        Assert.DoesNotContain("<html", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<!doctype", content, StringComparison.OrdinalIgnoreCase);

        // Body must conform to RFC 9457 Problem Details contract
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("type", out var typeProp));
        Assert.False(string.IsNullOrWhiteSpace(typeProp.GetString()));

        Assert.True(root.TryGetProperty("title", out var titleProp));
        Assert.Equal("Not Found", titleProp.GetString());

        Assert.True(root.TryGetProperty("status", out var statusProp));
        Assert.Equal(404, statusProp.GetInt32());

        Assert.True(root.TryGetProperty("instance", out var instanceProp));
        Assert.Equal(apiPath, instanceProp.GetString());

        Assert.True(root.TryGetProperty("traceId", out var traceIdProp));
        Assert.False(string.IsNullOrWhiteSpace(traceIdProp.GetString()));
    }

    [Fact]
    public async Task LiveHealthEndpoint_Returns200Ok_WithJson()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("status", out var statusProp));
        Assert.Equal("live", statusProp.GetString());
    }

    [Fact]
    public async Task WhenWebNotBuilt_HostStartsGracefully_AndApiRoutesFunction()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "rag-unbuilt-" + Guid.NewGuid().ToString("N"));

        using var unbuiltFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Spa:Root"] = nonExistentPath
                });
            });
        });

        using var client = unbuiltFactory.CreateClient();

        // Health endpoint still returns 200 OK
        var healthResponse = await client.GetAsync("/api/v1/system/health/live");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        // Unknown API route still returns 404 Problem Details
        var unknownApiResponse = await client.GetAsync("/api/v1/system/unknown-route");
        Assert.Equal(HttpStatusCode.NotFound, unknownApiResponse.StatusCode);
        Assert.Equal(ProblemDetailsExtensions.ProblemJsonMediaType, unknownApiResponse.Content.Headers.ContentType?.MediaType);
    }

    public void Dispose()
    {
        if (_tempSpaDir != null && Directory.Exists(_tempSpaDir))
        {
            try
            {
                Directory.Delete(_tempSpaDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup of temporary test directory
            }
        }

        GC.SuppressFinalize(this);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "RagPipeline.slnx")))
            {
                return dir;
            }

            var parent = Directory.GetParent(dir);
            if (parent == null)
            {
                break;
            }

            dir = parent.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root containing RagPipeline.slnx.");
    }
}
