namespace RagPipeline.IntegrationTests.Bootstrap;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using RagPipeline.BuildingBlocks.Persistence;
using RagPipeline.IntegrationTests.Persistence;
using RagPipeline.Persistence;
using RagPipeline.Persistence.Interceptors;
using Xunit;

[Collection("PostgresDatabase")]
[Trait("Category", "Bootstrap")]
public class BootstrapStatusEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;

    public BootstrapStatusEndpointTests(PostgresFixture fixture, WebApplicationFactory<Program> factory)
    {
        _fixture = fixture;
        _factory = factory;
    }

    private WebApplicationFactory<Program> CreateFactory(string? dbConnectionString = null, string? deploymentMode = null)
    {
        var effectiveConnStr = !string.IsNullOrWhiteSpace(dbConnectionString)
            ? dbConnectionString
            : (!string.IsNullOrWhiteSpace(_fixture.RuntimeConnectionString)
                ? _fixture.RuntimeConnectionString
                : "Host=localhost;Database=ragpipeline;Username=rag_app;Password=test");

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var dict = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] = effectiveConnStr,
                    ["ConnectionStrings:Maintenance"] = !string.IsNullOrWhiteSpace(_fixture.MigratorConnectionString) ? _fixture.MigratorConnectionString : effectiveConnStr,
                    ["Hosting:ApplicationName"] = "RagPipeline.Api"
                };
                if (deploymentMode != null)
                {
                    dict["Hosting:DeploymentMode"] = deploymentMode;
                }

                config.AddInMemoryCollection(dict);
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<RagPipelineDbContext>>();
                services.RemoveAll<RagPipelineDbContext>();
                services.AddDbContext<RagPipelineDbContext>((sp, options) =>
                {
                    options.UseNpgsql(effectiveConnStr, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                    });
                    options.AddInterceptors(
                        sp.GetRequiredService<TenantSecurityCommandInterceptor>(),
                        sp.GetRequiredService<PooledConnectionResetInterceptor>());
                });

                services.RemoveAll<IBootstrapStateReader>();
                services.RemoveAll<IBootstrapStateService>();
                services.AddScoped<IBootstrapStateReader, BootstrapStateReader>();
                services.AddScoped<IBootstrapStateService, BootstrapStateService>();

                if (deploymentMode != null)
                {
                    services.Configure<RagPipeline.BuildingBlocks.Configuration.HostingOptions>(options =>
                    {
                        options.DeploymentMode = deploymentMode;
                    });
                }
            });
        });
    }

    private async Task SetBootstrapStatusAsync(string status, Guid? sealedByUserId = null)
    {
        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            ALTER TABLE deployment_bootstrap_state DISABLE TRIGGER trg_bootstrap_state_irreversible;
            UPDATE deployment_bootstrap_state 
            SET status = @status, 
                sealed_at = CASE WHEN @status = 'SEALED' THEN clock_timestamp() ELSE NULL END,
                sealed_by_user_id = @userId
            WHERE id = 1;
            ALTER TABLE deployment_bootstrap_state ENABLE TRIGGER trg_bootstrap_state_irreversible;
        ";
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("userId", (object?)sealedByUserId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task GetBootstrapStatus_Anonymous_Returns200WithOnlyStateField()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await SetBootstrapStatusAsync("UNINITIALIZED");

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/bootstrap/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // Verify allowlist: JSON must contain ONLY the 'state' property
        var propertyNames = root.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Single(propertyNames);
        Assert.Equal("state", propertyNames[0]);
        Assert.Equal("UNINITIALIZED", root.GetProperty("state").GetString());

        // Explicit negative assertions against disallowed metadata leaks
        Assert.False(root.TryGetProperty("proof", out _));
        Assert.False(root.TryGetProperty("proofHash", out _));
        Assert.False(root.TryGetProperty("bootstrapProofHash", out _));
        Assert.False(root.TryGetProperty("expiresAt", out _));
        Assert.False(root.TryGetProperty("userCount", out _));
        Assert.False(root.TryGetProperty("tenantCount", out _));
        Assert.False(root.TryGetProperty("version", out _));
        Assert.False(root.TryGetProperty("host", out _));
        Assert.False(root.TryGetProperty("connectionString", out _));
    }

    [Fact]
    public async Task Status_503_IsProblemDetails_NoStackTrace()
    {
        // Point to unreachable database port
        using var factory = CreateFactory("Host=127.0.0.1;Port=59999;Database=fake;Username=fake;Password=fake;Timeout=1;");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/bootstrap/status");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("status", out var statusProp));
        Assert.Equal(503, statusProp.GetInt32());

        // Must not contain stack trace or internal exception details
        Assert.DoesNotContain("StackTrace", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at RagPipeline", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql.", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Status_RateLimitedOrCacheControlNoStore()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/bootstrap/status");

        Assert.NotNull(response.Headers.CacheControl);
        Assert.True(response.Headers.CacheControl.NoStore, "Status endpoint must specify Cache-Control: no-store.");
    }

    [Fact]
    public async Task Status_Post_Put_Patch_Delete_Return405_NoStateChange()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await SetBootstrapStatusAsync("UNINITIALIZED");

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var jsonContent = new StringContent("{\"state\":\"SEALED\"}", Encoding.UTF8, "application/json");

        var postRes = await client.PostAsync("/api/v1/bootstrap/status", jsonContent);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, postRes.StatusCode);

        var putRes = await client.PutAsync("/api/v1/bootstrap/status", jsonContent);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, putRes.StatusCode);

        var patchRes = await client.PatchAsync("/api/v1/bootstrap/status", jsonContent);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, patchRes.StatusCode);

        var deleteRes = await client.DeleteAsync("/api/v1/bootstrap/status");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, deleteRes.StatusCode);

        // Verify state remains UNINITIALIZED in database
        var getRes = await client.GetAsync("/api/v1/bootstrap/status");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        using var doc = JsonDocument.Parse(await getRes.Content.ReadAsStringAsync());
        Assert.Equal("UNINITIALIZED", doc.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void NoRoute_CanResetState()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var endpointSources = factory.Services.GetServices<EndpointDataSource>();
        var endpoints = endpointSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>().ToList();

        Assert.NotEmpty(endpoints);

        foreach (var endpoint in endpoints)
        {
            var pattern = endpoint.RoutePattern.RawText ?? string.Empty;

            // Assert no endpoint exposes a reset, unseal, or clear bootstrap route
            Assert.DoesNotContain("reset", pattern, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("unseal", pattern, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("uninitialize", pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Status_FreshBrowserNoCookies_SameAsLoggedIn_AfterSeal()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await SetBootstrapStatusAsync("SEALED");

        using var factory = CreateFactory();

        // 1. Client with simulated session cookies
        using var clientWithCookies = factory.CreateClient();
        clientWithCookies.DefaultRequestHeaders.Add("Cookie", "session=abc123xyz; user=some_user");

        var responseWithCookies = await clientWithCookies.GetAsync("/api/v1/bootstrap/status");
        Assert.Equal(HttpStatusCode.OK, responseWithCookies.StatusCode);
        using var doc1 = JsonDocument.Parse(await responseWithCookies.Content.ReadAsStringAsync());
        Assert.Equal("SEALED", doc1.RootElement.GetProperty("state").GetString());

        // 2. Fresh client with no cookies
        using var freshClient = factory.CreateClient();
        var responseFresh = await freshClient.GetAsync("/api/v1/bootstrap/status");
        Assert.Equal(HttpStatusCode.OK, responseFresh.StatusCode);
        using var doc2 = JsonDocument.Parse(await responseFresh.Content.ReadAsStringAsync());
        Assert.Equal("SEALED", doc2.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Status_AfterLogout_StillSealed()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await SetBootstrapStatusAsync("SEALED");

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Access without authentication credentials (simulating post-logout anonymous request)
        var response = await client.GetAsync("/api/v1/bootstrap/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("SEALED", doc.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Status_NoDatabase_Returns503Unavailable()
    {
        using var factory = CreateFactory("Host=127.0.0.1;Port=59999;Database=fake;Username=fake;Password=fake;Timeout=1;");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/bootstrap/status");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Status_MissingMigrationHistory_Returns503()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        var tempDb = $"status_no_hist_{Guid.NewGuid():N}";
        await using var adminConn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await adminConn.OpenAsync();
        await using (var cmd = adminConn.CreateCommand())
        {
            cmd.CommandText = $"CREATE DATABASE \"{tempDb}\";";
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(_fixture.SuperuserConnectionString) { Database = tempDb };
            using var factory = CreateFactory(builder.ConnectionString);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/v1/bootstrap/status");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            await using var dropCmd = adminConn.CreateCommand();
            dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{tempDb}\" WITH (FORCE);";
            await dropCmd.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Status_MigrationMismatch_Returns503()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        var tempDb = $"status_mismatch_{Guid.NewGuid():N}";
        await using var adminConn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await adminConn.OpenAsync();
        await using (var cmd = adminConn.CreateCommand())
        {
            cmd.CommandText = $"CREATE DATABASE \"{tempDb}\";";
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(_fixture.SuperuserConnectionString) { Database = tempDb };
            var options = new DbContextOptionsBuilder<RagPipelineDbContext>()
                .UseNpgsql(builder.ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
                .Options;

            await using (var context = new RagPipelineDbContext(options))
            {
                var migrator = context.GetService<IMigrator>();
                await migrator.MigrateAsync("InitialS0Schema");
            }

            using var factory = CreateFactory(builder.ConnectionString);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/v1/bootstrap/status");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            await using var dropCmd = adminConn.CreateCommand();
            dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{tempDb}\" WITH (FORCE);";
            await dropCmd.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Status_PermissionDenied_Returns503()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        var restrictedUser = $"status_denied_{Guid.NewGuid():N}";
        await using var adminConn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await adminConn.OpenAsync();
        await using (var cmd = adminConn.CreateCommand())
        {
            cmd.CommandText = $@"
                CREATE ROLE ""{restrictedUser}"" WITH LOGIN PASSWORD 'restricted_pass';
                GRANT USAGE ON SCHEMA public TO ""{restrictedUser}"";
                GRANT SELECT ON ""__EFMigrationsHistory"" TO ""{restrictedUser}"";
            ";
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(_fixture.SuperuserConnectionString)
            {
                Username = restrictedUser,
                Password = "restricted_pass"
            };

            using var factory = CreateFactory(builder.ConnectionString);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/v1/bootstrap/status");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            await using var dropCmd = adminConn.CreateCommand();
            dropCmd.CommandText = $@"
                DROP OWNED BY ""{restrictedUser}"" CASCADE;
                DROP ROLE IF EXISTS ""{restrictedUser}"";
            ";
            await dropCmd.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Status_DbRecovers_ReturnsRealState_NoStickyCache()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await SetBootstrapStatusAsync("UNINITIALIZED");

        // 1. Initial call returns real UNINITIALIZED
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var res1 = await client.GetAsync("/api/v1/bootstrap/status");
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        using var doc1 = JsonDocument.Parse(await res1.Content.ReadAsStringAsync());
        Assert.Equal("UNINITIALIZED", doc1.RootElement.GetProperty("state").GetString());

        // 2. Transition state to SEALED
        await SetBootstrapStatusAsync("SEALED");

        // 3. Immediate subsequent call must return SEALED (no sticky cache of UNINITIALIZED)
        var res2 = await client.GetAsync("/api/v1/bootstrap/status");
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        using var doc2 = JsonDocument.Parse(await res2.Content.ReadAsStringAsync());
        Assert.Equal("SEALED", doc2.RootElement.GetProperty("state").GetString());
    }
}
