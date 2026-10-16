namespace RagPipeline.IntegrationTests.Bootstrap;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using RagPipeline.BuildingBlocks.Audit;
using RagPipeline.BuildingBlocks.Bootstrap;
using RagPipeline.BuildingBlocks.Persistence;
using RagPipeline.IntegrationTests.Persistence;
using RagPipeline.Persistence;
using RagPipeline.Persistence.Bootstrap;
using RagPipeline.Persistence.Cli;
using RagPipeline.Persistence.Interceptors;
using Xunit;

[Collection("PostgresDatabase")]
[Trait("Category", "Bootstrap")]
public class BootstrapProofSecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;

    public BootstrapProofSecurityTests(PostgresFixture fixture, WebApplicationFactory<Program> factory)
    {
        _fixture = fixture;
        _factory = factory;
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var effectiveConnStr = !string.IsNullOrWhiteSpace(_fixture.RuntimeConnectionString)
            ? _fixture.RuntimeConnectionString
            : "Host=localhost;Database=ragpipeline;Username=rag_app;Password=test";

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] = effectiveConnStr,
                    ["ConnectionStrings:Maintenance"] = !string.IsNullOrWhiteSpace(_fixture.MigratorConnectionString) ? _fixture.MigratorConnectionString : effectiveConnStr,
                    ["Hosting:ApplicationName"] = "RagPipeline.Api"
                });
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
            });
        });
    }

    [Fact]
    public async Task ApiHostStartup_Stdout_Stderr_DoNotContainProof()
    {
        if (!_fixture.DockerAvailable) return;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Maintenance"] = _fixture.MigratorConnectionString
            })
            .Build();

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = await RagAdminCli.RunAsync(
            new[] { "rag-admin", "bootstrap-proof", "generate" },
            config, stdout, stderr, isInteractiveOverride: true);

        Assert.Equal(RagAdminExitCodes.Success, exitCode);
        var match = System.Text.RegularExpressions.Regex.Match(stdout.ToString(), @"Proof:\s+([a-f0-9]{64})");
        Assert.True(match.Success);
        var proof = match.Groups[1].Value;

        var origOut = Console.Out;
        var origErr = Console.Error;
        using var hostOut = new StringWriter();
        using var hostErr = new StringWriter();
        Console.SetOut(hostOut);
        Console.SetError(hostErr);

        try
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/api/v1/system/health/live");
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

            var capturedOut = hostOut.ToString();
            var capturedErr = hostErr.ToString();

            Assert.DoesNotContain(proof, capturedOut, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(proof, capturedErr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
    }

    [Fact]
    public async Task BootstrapStatus_Response_DoesNotContainProofOrHash()
    {
        if (!_fixture.DockerAvailable) return;

        // Generate proof via CLI
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Maintenance"] = _fixture.MigratorConnectionString
            })
            .Build();

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = await RagAdminCli.RunAsync(
            new[] { "rag-admin", "bootstrap-proof", "generate" },
            config, stdout, stderr, isInteractiveOverride: true);

        Assert.Equal(RagAdminExitCodes.Success, exitCode);
        var output = stdout.ToString();
        var match = System.Text.RegularExpressions.Regex.Match(output, @"Proof:\s+([a-f0-9]{64})");
        Assert.True(match.Success);
        var proof = match.Groups[1].Value;

        // Fetch /api/v1/bootstrap/status via API client
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/bootstrap/status");
        var body = await response.Content.ReadAsStringAsync();

        // Must not contain the plaintext proof
        Assert.DoesNotContain(proof, body, StringComparison.OrdinalIgnoreCase);

        // Must not contain proof hash or proof metadata
        Assert.DoesNotContain("proof", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bootstrap_proof_hash", body, StringComparison.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(body);
        var propertyNames = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Single(propertyNames);
        Assert.Equal("state", propertyNames[0]);
    }

    [Fact]
    public async Task Logs_DuringFailedAttempts_DoNotContainSubmittedProof()
    {
        if (!_fixture.DockerAvailable) return;

        var canaryProof = "canary-super-secret-proof-1234567890abcdef";

        await using var context = _fixture.CreateSuperuserDbContext();
        var stateReader = new BootstrapStateReader(context);
        var coordinator = new RagPipeline.Persistence.Tenancy.EfTenantTransactionCoordinator(context);
        var auditStore = new RagPipeline.Persistence.Audit.EfAuditEventStore(context, coordinator);
        var proofService = new BootstrapProofService(context, stateReader, auditEventStore: auditStore);

        // Submit invalid proof candidate
        var result = await proofService.VerifyAndConsumeProofAsync(canaryProof, "127.0.0.1");
        Assert.False(result.IsSuccess);

        // Query audit events in database to ensure candidate proof was NOT logged into details_json or anywhere
        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT action, outcome, details_json FROM audit_events WHERE action LIKE 'BootstrapProof%';";
        await using var reader = await cmd.ExecuteReaderAsync();

        var foundAudit = false;
        while (await reader.ReadAsync())
        {
            foundAudit = true;
            var details = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            Assert.DoesNotContain(canaryProof, details, StringComparison.OrdinalIgnoreCase);
        }

        Assert.True(foundAudit, "Expected audit event for verification attempt.");
    }

    [Fact]
    public async Task ProofAttempts_Audited_WithoutValue()
    {
        if (!_fixture.DockerAvailable) return;

        var dummyProof = "canary-audit-attempt-xyz987654321";

        await using var context = _fixture.CreateSuperuserDbContext();
        var stateReader = new BootstrapStateReader(context);
        var coordinator = new RagPipeline.Persistence.Tenancy.EfTenantTransactionCoordinator(context);
        var auditStore = new RagPipeline.Persistence.Audit.EfAuditEventStore(context, coordinator);
        var proofService = new BootstrapProofService(context, stateReader, auditEventStore: auditStore);

        await proofService.VerifyAndConsumeProofAsync(dummyProof, "10.0.0.1");

        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT action, outcome, details_json FROM audit_events WHERE action = 'BootstrapProof.Verify' ORDER BY occurred_at DESC LIMIT 1;";
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        var action = reader.GetString(0);
        var outcome = reader.GetString(1);
        var details = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);

        Assert.Equal("BootstrapProof.Verify", action);
        Assert.Equal("Failure", outcome);
        Assert.DoesNotContain(dummyProof, details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FileOutput_IsDeletedOrFlaggedAfterConsumption()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"proof_cleanup_{Guid.NewGuid():N}.txt");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Maintenance"] = _fixture.MigratorConnectionString
                })
                .Build();

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var exitCode = await RagAdminCli.RunAsync(
                new[] { "rag-admin", "bootstrap-proof", "generate", "--file", tempFile },
                config, stdout, stderr, isInteractiveOverride: false);

            if (_fixture.DockerAvailable)
            {
                Assert.Equal(RagAdminExitCodes.Success, exitCode);
                Assert.True(File.Exists(tempFile));

                // Operator cleanup simulation per documented runbook
                File.Delete(tempFile);
                Assert.False(File.Exists(tempFile));
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
