namespace RagPipeline.IntegrationTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Xunit;

public class OpenApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string DocumentName = "v1";
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hosting:ApplicationName"] = "RagPipeline.Api"
                });
            });
        });
    }

    [Fact]
    public async Task OpenApiSpec_MatchesCommittedContract()
    {
        var generatedSpec = await GenerateOpenApiSpecAsync();
        var repoRoot = FindRepoRoot();
        var contractPath = Path.Combine(repoRoot, "contracts", "openapi", "rag-pipeline.v1.json");

        var updateEnv = Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SPEC");
        var shouldUpdate = string.Equals(updateEnv, "1", StringComparison.Ordinal);

        if (shouldUpdate)
        {
            var dir = Path.GetDirectoryName(contractPath)!;
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(contractPath, generatedSpec, new UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(contractPath),
            $"Committed OpenAPI contract file was not found at: {contractPath}\n" +
            "To generate the committed contract, run:\n" +
            "UPDATE_OPENAPI_SPEC=1 dotnet test tests/Integration/RagPipeline.IntegrationTests --filter \"FullyQualifiedName~OpenApiContractTests.OpenApiSpec_MatchesCommittedContract\"");

        var committedSpec = await File.ReadAllTextAsync(contractPath, Encoding.UTF8);
        var matches = CompareContracts(committedSpec, generatedSpec, out var difference);

        Assert.True(matches,
            $"OpenAPI specification drift detected!\n" +
            $"{difference}\n\n" +
            "To regenerate and update the committed contract, run:\n" +
            "UPDATE_OPENAPI_SPEC=1 dotnet test tests/Integration/RagPipeline.IntegrationTests --filter \"FullyQualifiedName~OpenApiContractTests.OpenApiSpec_MatchesCommittedContract\"");
    }

    [Fact]
    public async Task LiveAndReadyEndpoints_PathsExistInOpenApiDocument()
    {
        var document = await GetV1DocumentAsync();

        Assert.NotNull(document.Paths);
        Assert.True(document.Paths.ContainsKey("/api/v1/system/health/live"),
            "OpenAPI specification missing path '/api/v1/system/health/live'");
        Assert.True(document.Paths.ContainsKey("/api/v1/system/health/ready"),
            "OpenAPI specification missing path '/api/v1/system/health/ready'");

        var json = SerializeOpenApiDocument(document);
        using var jsonDoc = JsonDocument.Parse(json);
        var paths = jsonDoc.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/system/health/live", out _),
            "Serialized JSON missing path '/api/v1/system/health/live'");
        Assert.True(paths.TryGetProperty("/api/v1/system/health/ready", out _),
            "Serialized JSON missing path '/api/v1/system/health/ready'");
    }

    [Fact]
    public async Task ReadyEndpoint_Defines200And503Responses()
    {
        var document = await GetV1DocumentAsync();

        var json = SerializeOpenApiDocument(document);
        using var jsonDoc = JsonDocument.Parse(json);
        var readyPath = jsonDoc.RootElement.GetProperty("paths").GetProperty("/api/v1/system/health/ready");
        var getOperation = readyPath.GetProperty("get");
        var responses = getOperation.GetProperty("responses");

        Assert.True(responses.TryGetProperty("200", out _),
            "Ready endpoint GET operation missing 200 response");
        Assert.True(responses.TryGetProperty("503", out _),
            "Ready endpoint GET operation missing 503 response");
    }

    [Fact]
    public async Task BootstrapStatusEndpoint_ExistsInOpenApiDocument()
    {
        var document = await GetV1DocumentAsync();

        Assert.NotNull(document.Paths);
        Assert.True(document.Paths.ContainsKey("/api/v1/bootstrap/status"),
            "OpenAPI specification missing path '/api/v1/bootstrap/status'");

        var json = SerializeOpenApiDocument(document);
        using var jsonDoc = JsonDocument.Parse(json);
        var paths = jsonDoc.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/bootstrap/status", out var statusEndpoint),
            "Serialized JSON missing path '/api/v1/bootstrap/status'");

        var getOperation = statusEndpoint.GetProperty("get");
        var responses = getOperation.GetProperty("responses");

        Assert.True(responses.TryGetProperty("200", out _),
            "Bootstrap status endpoint GET operation missing 200 response");
        Assert.True(responses.TryGetProperty("503", out _),
            "Bootstrap status endpoint GET operation missing 503 response");
    }

    [Fact]
    public async Task ProblemResponses_UseProblemJson_AndSchemaHasTraceId()
    {
        var document = await GetV1DocumentAsync();

        var json = SerializeOpenApiDocument(document);
        using var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;
        var paths = root.GetProperty("paths");

        // Assert live endpoint problem response uses application/problem+json
        var liveGet = paths.GetProperty("/api/v1/system/health/live").GetProperty("get");
        var live500 = liveGet.GetProperty("responses").GetProperty("500");
        Assert.True(live500.GetProperty("content").TryGetProperty("application/problem+json", out _),
            "Live endpoint 500 response does not use application/problem+json");

        // Assert ready endpoint problem response uses application/problem+json
        var readyGet = paths.GetProperty("/api/v1/system/health/ready").GetProperty("get");
        var ready500 = readyGet.GetProperty("responses").GetProperty("500");
        Assert.True(ready500.GetProperty("content").TryGetProperty("application/problem+json", out _),
            "Ready endpoint 500 response does not use application/problem+json");

        // Assert ProblemDetails schema in components.schemas contains traceId
        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty("ProblemDetails", out var problemDetailsSchema),
            "components.schemas missing 'ProblemDetails'");
        var properties = problemDetailsSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("traceId", out var traceIdSchema),
            "ProblemDetails schema properties missing 'traceId'");
        Assert.Equal("string", traceIdSchema.GetProperty("type").GetString());

        // Object model assertions
        Assert.NotNull(document.Components?.Schemas);
        Assert.True(document.Components.Schemas.TryGetValue("ProblemDetails", out var problemSchema));
        var concreteSchema = Assert.IsType<OpenApiSchema>(problemSchema);
        Assert.NotNull(concreteSchema.Properties);
        Assert.True(concreteSchema.Properties.ContainsKey("traceId"));
    }

    [Fact]
    public async Task OpenApiSpec_GenerationIsDeterministic()
    {
        var spec1 = await GenerateOpenApiSpecAsync();
        var spec2 = await GenerateOpenApiSpecAsync();

        Assert.Equal(spec1, spec2);
    }

    [Fact]
    public async Task DriftDetectionProof_MutatedSpec_ComparisonHelperReportsDifference()
    {
        var originalSpec = await GenerateOpenApiSpecAsync();

        // Mutate generated text (e.g. title)
        Assert.Contains("\"title\": \"RagPipeline API\"", originalSpec);
        var mutatedSpec = originalSpec.Replace("\"title\": \"RagPipeline API\"", "\"title\": \"RagPipeline API Mutated Title\"");
        Assert.NotEqual(originalSpec, mutatedSpec);

        var matches = CompareContracts(originalSpec, mutatedSpec, out var difference);
        Assert.False(matches, "Expected CompareContracts to report false when specification is mutated.");
        Assert.NotNull(difference);
        Assert.Contains("Difference detected at line", difference);
        Assert.Contains("RagPipeline API Mutated Title", difference);
    }

    internal static bool CompareContracts(string expected, string actual, out string? difference)
    {
        var normExpected = expected.Replace("\r\n", "\n").TrimEnd('\n');
        var normActual = actual.Replace("\r\n", "\n").TrimEnd('\n');

        if (string.Equals(normExpected, normActual, StringComparison.Ordinal))
        {
            difference = null;
            return true;
        }

        var expectedLines = normExpected.Split('\n');
        var actualLines = normActual.Split('\n');
        var minLines = Math.Min(expectedLines.Length, actualLines.Length);

        for (var i = 0; i < minLines; i++)
        {
            if (!string.Equals(expectedLines[i], actualLines[i], StringComparison.Ordinal))
            {
                difference = $"Difference detected at line {i + 1}:\n  Expected: {expectedLines[i]}\n  Actual:   {actualLines[i]}";
                return false;
            }
        }

        if (expectedLines.Length != actualLines.Length)
        {
            difference = $"Line count mismatch: expected {expectedLines.Length} lines, got {actualLines.Length} lines.";
            return false;
        }

        difference = "Content differed in unspecified byte sequence.";
        return false;
    }

    private async Task<string> GenerateOpenApiSpecAsync(CancellationToken cancellationToken = default)
    {
        var document = await GetV1DocumentAsync(cancellationToken);
        return SerializeOpenApiDocument(document);
    }

    private async Task<OpenApiDocument> GetV1DocumentAsync(CancellationToken cancellationToken = default)
    {
        using var client = _factory.CreateClient();
        var provider = _factory.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(DocumentName);
        return await provider.GetOpenApiDocumentAsync(cancellationToken);
    }

    private static string SerializeOpenApiDocument(OpenApiDocument document)
    {
        using var stringWriter = new StringWriter { NewLine = "\n" };
        var jsonWriter = new OpenApiJsonWriter(stringWriter);
        document.SerializeAsV31(jsonWriter);

        var output = stringWriter.ToString();
        return output.Replace("\r\n", "\n").TrimEnd('\n') + "\n";
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
