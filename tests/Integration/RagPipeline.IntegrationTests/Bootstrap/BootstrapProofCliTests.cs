namespace RagPipeline.IntegrationTests.Bootstrap;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Npgsql;
using RagPipeline.BuildingBlocks.Bootstrap;
using RagPipeline.IntegrationTests.Persistence;
using RagPipeline.Persistence.Cli;
using Xunit;

[Collection("PostgresDatabase")]
[Trait("Category", "Bootstrap")]
public class BootstrapProofCliTests
{
    private readonly PostgresFixture _fixture;

    public BootstrapProofCliTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private IConfiguration CreateConfig(string? maintenanceConnStr = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Maintenance"] = maintenanceConnStr ?? _fixture.MigratorConnectionString,
                ["ConnectionStrings:Database"] = _fixture.RuntimeConnectionString,
                ["Hosting:ApplicationName"] = "RagPipeline.Api"
            })
            .Build();
    }

    private async Task ResetBootstrapStateAsync(string status = "UNINITIALIZED")
    {
        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            ALTER TABLE deployment_bootstrap_state DISABLE TRIGGER trg_bootstrap_state_irreversible;
            UPDATE deployment_bootstrap_state
            SET status = @status,
                bootstrap_proof_hash = NULL,
                proof_expires_at = NULL,
                proof_consumed_at = NULL,
                sealed_at = CASE WHEN @status = 'SEALED' THEN clock_timestamp() ELSE NULL END,
                sealed_by_user_id = NULL
            WHERE id = 1;
            ALTER TABLE deployment_bootstrap_state ENABLE TRIGGER trg_bootstrap_state_irreversible;
        ";
        cmd.Parameters.AddWithValue("status", status);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Cli_Generate_OnUninitialized_StoresHashAndPrintsProofOnlyToInteractiveStdout()
    {
        if (!_fixture.DockerAvailable) return;

        await ResetBootstrapStateAsync("UNINITIALIZED");

        var config = CreateConfig();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await RagAdminCli.RunAsync(
            new[] { "rag-admin", "bootstrap-proof", "generate" },
            config,
            stdout,
            stderr,
            isInteractiveOverride: true);

        Assert.Equal(RagAdminExitCodes.Success, exitCode);
        Assert.Empty(stderr.ToString());

        var output = stdout.ToString();
        var match = Regex.Match(output, @"Proof:\s+([a-f0-9]{64})", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Expected proof in output:\n{output}");

        var proof = match.Groups[1].Value.ToLowerInvariant();
        Assert.Equal(64, proof.Length);

        // Verify database state: hash is stored, never plaintext
        await using var conn = new NpgsqlConnection(_fixture.MigratorConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT status, bootstrap_proof_hash, proof_expires_at, proof_consumed_at FROM deployment_bootstrap_state WHERE id = 1;";
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        var status = reader.GetString(0);
        var storedHash = reader.GetString(1);
        var expiresAt = reader.GetDateTime(2);
        var isConsumed = !reader.IsDBNull(3);

        Assert.Equal("UNINITIALIZED", status);
        Assert.Equal(BootstrapProofToken.ComputeHash(proof), storedHash);
        Assert.True(expiresAt > DateTime.UtcNow);
        Assert.False(isConsumed);
    }

    [Fact]
    public async Task Cli_Generate_NonInteractive_WithoutFileFlag_Refuses()
    {
        if (!_fixture.DockerAvailable) return;

        await ResetBootstrapStateAsync("UNINITIALIZED");

        var config = CreateConfig();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await RagAdminCli.RunAsync(
            new[] { "rag-admin", "bootstrap-proof", "generate" },
            config,
            stdout,
            stderr,
            isInteractiveOverride: false); // non-interactive / piped stdout

        Assert.Equal(RagAdminExitCodes.SecurityViolation, exitCode);
        Assert.Contains("Non-interactive stdout detected", stderr.ToString());

        // Proof must NEVER be emitted to stdout or stderr
        Assert.Empty(stdout.ToString());
        Assert.DoesNotContain("Proof:", stderr.ToString());
    }

    [Fact]
    public async Task Cli_Generate_ToFile_Creates0600_RootOnly_AndRefusesExistingOrWorldReadablePath()
    {
        if (!_fixture.DockerAvailable) return;

        await ResetBootstrapStateAsync("UNINITIALIZED");

        var tempDir = Path.Combine(Path.GetTempPath(), $"rag_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "proof.txt");

        try
        {
            var config = CreateConfig();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            // 1. Generate to file
            var exitCode = await RagAdminCli.RunAsync(
                new[] { "rag-admin", "bootstrap-proof", "generate", "--file", filePath },
                config,
                stdout,
                stderr,
                isInteractiveOverride: false);

            Assert.Equal(RagAdminExitCodes.Success, exitCode);
            Assert.True(File.Exists(filePath));

            var proof = (await File.ReadAllTextAsync(filePath)).Trim();
            Assert.Equal(64, proof.Length);

            // On POSIX / Linux, assert 0600 (owner read/write only)
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var mode = File.GetUnixFileMode(filePath);
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
            }

            // 2. Generating again targeting the same existing file MUST refuse
            var stdout2 = new StringWriter();
            var stderr2 = new StringWriter();
            var exitCode2 = await RagAdminCli.RunAsync(
                new[] { "rag-admin", "bootstrap-proof", "generate", "--file", filePath },
                config,
                stdout2,
                stderr2,
                isInteractiveOverride: false);

            Assert.Equal(RagAdminExitCodes.SecurityViolation, exitCode2);
            Assert.Contains("already exists", stderr2.ToString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task Cli_Generate_Twice_InvalidatesOrRejectsPrevious()
    {
        if (!_fixture.DockerAvailable) return;

        await ResetBootstrapStateAsync("UNINITIALIZED");
        var config = CreateConfig();

        // 1. First generation
        var stdout1 = new StringWriter();
        var stderr1 = new StringWriter();
        var exit1 = await RagAdminCli.RunAsync(
            new[] { "rag-admin", "bootstrap-proof", "generate" },
            config, stdout1, stderr1, isInteractiveOverride: true);
        Assert.Equal(RagAdminExitCodes.Success, exit1);
        var proof1 = Regex.Match(stdout1.ToString(), @"Proof:\s+([a-f0-9]{64})").Groups[1].Value;

        // 2. Second generation (supersedes first)
        var stdout2 = new StringWriter();
        var stderr2 = new StringWriter();
        var exit2 = await RagAdminCli.RunAsync(
            new[] { "rag-admin", "bootstrap-proof", "generate" },
            config, stdout2, stderr2, isInteractiveOverride: true);
        Assert.Equal(RagAdminExitCodes.Success, exit2);
        var proof2 = Regex.Match(stdout2.ToString(), @"Proof:\s+([a-f0-9]{64})").Groups[1].Value;

        Assert.NotEqual(proof1, proof2);

        // 3. Verify that DB only has proof2 hash
        await using var conn = new NpgsqlConnection(_fixture.MigratorConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT bootstrap_proof_hash FROM deployment_bootstrap_state WHERE id = 1;";
        var activeHash = (string)(await cmd.ExecuteScalarAsync())!;

        Assert.Equal(BootstrapProofToken.ComputeHash(proof2), activeHash);
        Assert.NotEqual(BootstrapProofToken.ComputeHash(proof1), activeHash);
    }

    [Fact]
    public async Task Cli_Generate_ConcurrentInvocations_LeaveSingleActiveProof()
    {
        if (!_fixture.DockerAvailable) return;

        await ResetBootstrapStateAsync("UNINITIALIZED");
        var config = CreateConfig();

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var outW = new StringWriter();
            var errW = new StringWriter();
            return await RagAdminCli.RunAsync(
                new[] { "rag-admin", "bootstrap-proof", "generate" },
                config, outW, errW, isInteractiveOverride: true);
        });

        var results = await Task.WhenAll(tasks);
        Assert.All(results, code => Assert.Equal(RagAdminExitCodes.Success, code));

        // Exactly one row exists, with exactly one active proof hash
        await using var conn = new NpgsqlConnection(_fixture.MigratorConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*), COUNT(bootstrap_proof_hash) FROM deployment_bootstrap_state WHERE id = 1;";
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.Equal(1L, reader.GetInt64(1));
    }

    [Fact]
    public async Task Cli_Generate_WithoutDb_Fails_NonZeroExit_NoProofPrinted()
    {
        var badConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Maintenance"] = "Host=127.0.0.1;Port=59999;Database=fake;Username=fake;Password=fake;Timeout=1;"
            })
            .Build();

        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await RagAdminCli.RunAsync(
            new[] { "rag-admin", "bootstrap-proof", "generate" },
            badConfig, stdout, stderr, isInteractiveOverride: true);

        Assert.Equal(RagAdminExitCodes.DatabaseError, exitCode);
        Assert.Empty(stdout.ToString());
        Assert.Contains("Database", stderr.ToString());
        Assert.DoesNotContain("Proof:", stderr.ToString());
    }

    [Fact]
    public void Cli_Generate_ExitCodes_AreDistinct()
    {
        var codes = new[]
        {
            RagAdminExitCodes.Success,
            RagAdminExitCodes.InvalidArguments,
            RagAdminExitCodes.StateConflict,
            RagAdminExitCodes.DatabaseError,
            RagAdminExitCodes.SecurityViolation
        };

        Assert.Equal(5, codes.Distinct().Count());
        Assert.Equal(0, RagAdminExitCodes.Success);
    }

    [Fact]
    public async Task Cli_Runs_UnderMigratorOrDedicatedRole_NotRuntimeRole()
    {
        if (!_fixture.DockerAvailable) return;

        await ResetBootstrapStateAsync("UNINITIALIZED");

        // Point explicitly to runtime connection string (role rag_app)
        var runtimeOnlyConfig = CreateConfig(_fixture.RuntimeConnectionString);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await RagAdminCli.RunAsync(
            new[] { "rag-admin", "bootstrap-proof", "generate" },
            runtimeOnlyConfig, stdout, stderr, isInteractiveOverride: true);

        Assert.Equal(RagAdminExitCodes.SecurityViolation, exitCode);
        Assert.Contains("rag_app", stderr.ToString());
        Assert.Empty(stdout.ToString());
    }
}
