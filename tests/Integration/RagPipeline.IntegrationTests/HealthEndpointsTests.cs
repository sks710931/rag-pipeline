namespace RagPipeline.IntegrationTests;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

public class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetLive_Returns200_WithStatusLive()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("status", out var statusProp));
        Assert.Equal("live", statusProp.GetString());
    }

    [Fact]
    public async Task GetReady_WhenOptionalDependenciesAbsent_ReturnsReadyWithNotConfigured()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.Equal("ready", root.GetProperty("status").GetString());

        var dependencies = root.GetProperty("dependencies");
        Assert.Equal("configured", dependencies.GetProperty("database").GetString());
        Assert.Equal("not_configured", dependencies.GetProperty("qdrant").GetString());
        Assert.Equal("not_configured", dependencies.GetProperty("ai").GetString());
        Assert.Equal("not_configured", dependencies.GetProperty("azure").GetString());
    }

    [Fact]
    public async Task GetReady_WhenOptionalDependenciesConfigured_ReturnsConfiguredWithoutEchoingValues()
    {
        const string fakeQdrantKey = "qdrant-secret-key-123";
        const string fakeAiKey = "sk-fake-ai-secret-456";
        const string fakeAzureKey = "azure-ocr-key-789";

        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Qdrant:Endpoint"] = "http://localhost:6333",
                    ["Qdrant:ApiKey"] = fakeQdrantKey,
                    ["Ai:ApiKey"] = fakeAiKey,
                    ["Azure:ApiKey"] = fakeAzureKey
                });
            });
        });

        using var client = customFactory.CreateClient();
        var response = await client.GetAsync("/api/v1/system/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.Equal("ready", root.GetProperty("status").GetString());

        var dependencies = root.GetProperty("dependencies");
        Assert.Equal("configured", dependencies.GetProperty("database").GetString());
        Assert.Equal("configured", dependencies.GetProperty("qdrant").GetString());
        Assert.Equal("configured", dependencies.GetProperty("ai").GetString());
        Assert.Equal("configured", dependencies.GetProperty("azure").GetString());

        // Strictly verify that raw values and secrets are never echoed in response
        Assert.DoesNotContain(fakeQdrantKey, content);
        Assert.DoesNotContain(fakeAiKey, content);
        Assert.DoesNotContain(fakeAzureKey, content);
    }
}
