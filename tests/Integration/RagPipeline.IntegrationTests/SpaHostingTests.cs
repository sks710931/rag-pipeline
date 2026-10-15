namespace RagPipeline.IntegrationTests;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RagPipeline.Api;
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
            // and dummy asset file so SPA hosting, asset serving, and fallback tests can run reliably.
            _tempSpaDir = Path.Combine(Path.GetTempPath(), "rag-spa-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempSpaDir);
            File.WriteAllText(
                Path.Combine(_tempSpaDir, "index.html"),
                "<!doctype html><html lang=\"en\"><head><title>Pixel RAG Test SPA</title></head><body><div id=\"root\"></div></body></html>");

            var assetsDir = Path.Combine(_tempSpaDir, "assets");
            Directory.CreateDirectory(assetsDir);
            File.WriteAllText(Path.Combine(assetsDir, "bundle-test12345.js"), "console.log('test asset');");

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

        AssertResponseHeaders(response, isIndexHtml: true, isAsset: false);
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

        AssertResponseHeaders(response, isIndexHtml: true, isAsset: false);
    }

    [Theory]
    [InlineData("/some/route")]
    [InlineData("/")]
    [InlineData("/app/status")]
    public async Task PostToClientRoute_Returns405ProblemDetails_NeverIndexHtml(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync(path, null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(ProblemDetailsExtensions.ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<html", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<!doctype", content, StringComparison.OrdinalIgnoreCase);

        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("type", out var typeProp));
        Assert.False(string.IsNullOrWhiteSpace(typeProp.GetString()));

        Assert.True(root.TryGetProperty("title", out var titleProp));
        Assert.False(string.IsNullOrWhiteSpace(titleProp.GetString()));

        Assert.True(root.TryGetProperty("status", out var statusProp));
        Assert.Equal(405, statusProp.GetInt32());

        Assert.True(root.TryGetProperty("instance", out var instanceProp));
        Assert.Equal(path, instanceProp.GetString());

        Assert.True(root.TryGetProperty("traceId", out var traceIdProp));
        Assert.False(string.IsNullOrWhiteSpace(traceIdProp.GetString()));
    }

    [Theory]
    [InlineData("PUT", "/some/route")]
    [InlineData("PATCH", "/some/route")]
    [InlineData("DELETE", "/some/route")]
    [InlineData("PUT", "/")]
    [InlineData("DELETE", "/")]
    public async Task MutationMethodsToClientRoute_Return405ProblemDetails_NeverIndexHtml(string method, string path)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(ProblemDetailsExtensions.ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<html", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<!doctype", content, StringComparison.OrdinalIgnoreCase);

        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;
        Assert.True(root.TryGetProperty("status", out var statusProp));
        Assert.Equal(405, statusProp.GetInt32());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/some/route")]
    [InlineData("/app/status")]
    public async Task GetClientRoute_WhenNotAcceptingHtml_Returns404ProblemDetails_NeverIndexHtml(string path)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd("application/json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemDetailsExtensions.ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<html", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<!doctype", content, StringComparison.OrdinalIgnoreCase);

        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;
        Assert.True(root.TryGetProperty("status", out var statusProp));
        Assert.Equal(404, statusProp.GetInt32());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/app/status")]
    public async Task HeadClientRoute_ServesHeaders_With200AndEmptyBody(string path)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Head, path);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Empty(content);

        AssertResponseHeaders(response, isIndexHtml: true, isAsset: false);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/app/status")]
    public async Task SpaIndexHtml_ResponseHeaders_AssertRequiredSecurityAndCacheHeaders(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        AssertResponseHeaders(response, isIndexHtml: true, isAsset: false);
    }

    [Fact]
    public async Task StaticAsset_ResponseHeaders_AssertRequiredSecurityAndCacheHeaders()
    {
        using var client = _factory.CreateClient();
        var assetPath = GetAvailableAssetPath();

        var response = await client.GetAsync(assetPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertResponseHeaders(response, isIndexHtml: false, isAsset: true);
    }

    [Theory]
    [InlineData("/api/v1/system/unknown-route-that-does-not-exist")]
    [InlineData("/api/v1/nonexistent")]
    [InlineData("/api/unknown")]
    [InlineData("/api")]
    [InlineData("/api/")]
    [InlineData("/api%2fv1/nonexistent")]
    [InlineData("/api%2Fsystem")]
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

    private static void AssertResponseHeaders(HttpResponseMessage response, bool isIndexHtml, bool isAsset)
    {
        // X-Content-Type-Options: nosniff
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var xctoValues));
        Assert.Equal(SpaHostingExtensions.XContentTypeOptionsHeaderValue, xctoValues.Single());

        // Referrer-Policy: strict-origin-when-cross-origin
        Assert.True(response.Headers.TryGetValues("Referrer-Policy", out var rpValues));
        Assert.Equal(SpaHostingExtensions.ReferrerPolicyHeaderValue, rpValues.Single());

        // X-Frame-Options: DENY
        Assert.True(response.Headers.TryGetValues("X-Frame-Options", out var xfoValues));
        Assert.Equal(SpaHostingExtensions.XFrameOptionsHeaderValue, xfoValues.Single());

        // Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'
        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var cspValues));
        Assert.Equal(SpaHostingExtensions.ContentSecurityPolicyHeaderValue, cspValues.Single());

        if (isIndexHtml)
        {
            // index.html Cache-Control: no-cache
            Assert.True(response.Headers.TryGetValues("Cache-Control", out var ccValues));
            Assert.Equal(SpaHostingExtensions.IndexCacheControlHeaderValue, ccValues.Single());
        }

        if (isAsset)
        {
            // hashed files under /assets/ Cache-Control: public, max-age=31536000, immutable
            Assert.True(response.Headers.TryGetValues("Cache-Control", out var ccValues));
            var cacheControl = ccValues.Single();
            Assert.Equal(SpaHostingExtensions.AssetsCacheControlHeaderValue, cacheControl);
        }
    }

    private string GetAvailableAssetPath()
    {
        var spaRoot = _tempSpaDir ?? Path.Combine(FindRepoRoot(), "apps", "web", "dist");
        var assetsDir = Path.Combine(spaRoot, "assets");
        if (Directory.Exists(assetsDir))
        {
            var firstFile = Directory.GetFiles(assetsDir).FirstOrDefault();
            if (firstFile != null)
            {
                return "/assets/" + Path.GetFileName(firstFile);
            }
        }

        throw new InvalidOperationException("No static asset file found in SPA root assets directory.");
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
