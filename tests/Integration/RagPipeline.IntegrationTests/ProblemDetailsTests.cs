namespace RagPipeline.IntegrationTests;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RagPipeline.BuildingBlocks.ErrorHandling;
using RagPipeline.BuildingBlocks.Modules;
using Xunit;

public class ProblemDetailsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProblemDetailsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetailsShape_WithNoStackTrace()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/unknown-route-that-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemDetailsExtensions.ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("type", out var typeProp));
        Assert.False(string.IsNullOrWhiteSpace(typeProp.GetString()));

        Assert.True(root.TryGetProperty("title", out var titleProp));
        Assert.Equal("Not Found", titleProp.GetString());

        Assert.True(root.TryGetProperty("status", out var statusProp));
        Assert.Equal(404, statusProp.GetInt32());

        Assert.True(root.TryGetProperty("traceId", out var traceIdProp));
        Assert.False(string.IsNullOrWhiteSpace(traceIdProp.GetString()));

        // Verify no stack trace or internal exception details are leaked
        Assert.DoesNotContain("stackTrace", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", content, StringComparison.OrdinalIgnoreCase);
        Assert.False(root.TryGetProperty("exception", out _));
    }

    [Fact]
    public async Task ThrownException_ReturnsProblemDetailsShape_WithNoStackTraceOrExceptionMessage()
    {
        const string sensitiveExceptionMessage = "SensitiveInternalDatabaseCredentialsOrCrashDetail";

        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Add test-only endpoint via WebApplicationFactory configuration without touching production code
                services.AddSingleton<IModule>(new TestFaultyModule(sensitiveExceptionMessage));
            });
        });

        using var client = customFactory.CreateClient();

        var response = await client.GetAsync("/api/v1/test/throw-exception");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ProblemDetailsExtensions.ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("type", out var typeProp));
        Assert.False(string.IsNullOrWhiteSpace(typeProp.GetString()));

        Assert.True(root.TryGetProperty("title", out var titleProp));
        Assert.Equal("An error occurred while processing your request.", titleProp.GetString());

        Assert.True(root.TryGetProperty("status", out var statusProp));
        Assert.Equal(500, statusProp.GetInt32());

        Assert.True(root.TryGetProperty("traceId", out var traceIdProp));
        Assert.False(string.IsNullOrWhiteSpace(traceIdProp.GetString()));

        // Strictly verify that sensitive exception message and stack trace are NEVER leaked
        Assert.DoesNotContain(sensitiveExceptionMessage, content);
        Assert.DoesNotContain("stackTrace", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", content, StringComparison.OrdinalIgnoreCase);
        Assert.False(root.TryGetProperty("exception", out _));
    }

    private sealed class TestFaultyModule(string exceptionMessage) : IModule
    {
        public string Name => "TestFaulty";

        public void RegisterModule(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/v1/test/throw-exception", () =>
            {
                throw new InvalidOperationException(exceptionMessage);
            });
        }
    }
}
