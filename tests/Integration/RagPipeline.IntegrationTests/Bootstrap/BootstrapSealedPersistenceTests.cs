namespace RagPipeline.IntegrationTests.Bootstrap;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using RagPipeline.BuildingBlocks.Configuration;
using RagPipeline.IntegrationTests.Persistence;
using RagPipeline.Persistence;
using RagPipeline.Persistence.Entities;
using Xunit;

[Collection("PostgresDatabase")]
[Trait("Category", "Bootstrap")]
public class BootstrapSealedPersistenceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;

    public BootstrapSealedPersistenceTests(PostgresFixture fixture, WebApplicationFactory<Program> factory)
    {
        _fixture = fixture;
        _factory = factory;
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] = _fixture.RuntimeConnectionString,
                    ["ConnectionStrings:Maintenance"] = _fixture.MigratorConnectionString,
                    ["Hosting:ApplicationName"] = "RagPipeline.Api"
                });
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
    public async Task Schema_Singleton_SecondRowRejected()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO deployment_bootstrap_state (id, status) VALUES (2, 'UNINITIALIZED');";

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("23514", ex.SqlState); // check_violation
        Assert.Contains("chk_bootstrap_singleton", ex.Message);
    }

    [Fact]
    public async Task Transition_ToBootstrapping_IsAtomicUnderConcurrency()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await SetBootstrapStatusAsync("UNINITIALIZED");

        // 20 parallel attempts to begin bootstrapping; exactly one must win
        var attempts = Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var context = _fixture.CreateRuntimeDbContext();
            var service = new BootstrapStateService(
                context,
                new BootstrapStateReader(context),
                Options.Create(new HostingOptions()));

            return await service.TryBeginBootstrappingAsync();
        });

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(19, results.Count(r => !r));

        // Verify status in database is now BOOTSTRAPPING
        await using var verifyConn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await verifyConn.OpenAsync();
        await using var verifyCmd = verifyConn.CreateCommand();
        verifyCmd.CommandText = "SELECT status FROM deployment_bootstrap_state WHERE id = 1;";
        var status = (string)(await verifyCmd.ExecuteScalarAsync())!;
        Assert.Equal("BOOTSTRAPPING", status);
    }

    [Fact]
    public async Task Transition_Seal_IsOneWay_ByTrigger()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await SetBootstrapStatusAsync("SEALED");

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        // 1. UPDATE on a SEALED row must be rejected by trigger
        await using (var updateCmd = conn.CreateCommand())
        {
            updateCmd.CommandText = "UPDATE deployment_bootstrap_state SET status = 'UNINITIALIZED' WHERE id = 1;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => updateCmd.ExecuteNonQueryAsync());
            Assert.Contains("cannot be modified once SEALED", ex.Message);
        }

        // 2. DELETE on deployment_bootstrap_state must be rejected by trigger
        await using (var delCmd = conn.CreateCommand())
        {
            delCmd.CommandText = "DELETE FROM deployment_bootstrap_state WHERE id = 1;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => delCmd.ExecuteNonQueryAsync());
            Assert.Contains("cannot be deleted", ex.Message);
        }

        // 3. TRUNCATE must be rejected with permission denied (42501)
        await using (var truncCmd = conn.CreateCommand())
        {
            truncCmd.CommandText = "TRUNCATE TABLE deployment_bootstrap_state;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => truncCmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
    }

    [Fact]
    public async Task Sealed_SurvivesHostRestart()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await SetBootstrapStatusAsync("SEALED");

        // First host instance
        using (var factory1 = CreateFactory())
        {
            using var client1 = factory1.CreateClient();
            var res1 = await client1.GetAsync("/api/v1/bootstrap/status");
            Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
            using var doc1 = JsonDocument.Parse(await res1.Content.ReadAsStringAsync());
            Assert.Equal("SEALED", doc1.RootElement.GetProperty("state").GetString());
        }

        // Host restarted: dispose first factory and instantiate fresh second host instance
        using (var factory2 = CreateFactory())
        {
            using var client2 = factory2.CreateClient();
            var res2 = await client2.GetAsync("/api/v1/bootstrap/status");
            Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
            using var doc2 = JsonDocument.Parse(await res2.Content.ReadAsStringAsync());
            Assert.Equal("SEALED", doc2.RootElement.GetProperty("state").GetString());
        }
    }

    [Fact]
    public async Task Sealed_SurvivesMigrationRerunAndUpgrade()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await SetBootstrapStatusAsync("SEALED");

        // Re-run migrations via superuser / migrator
        await using (var context = _fixture.CreateSuperuserDbContext())
        {
            await context.Database.MigrateAsync();
        }

        // Verify status remains SEALED
        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT status FROM deployment_bootstrap_state WHERE id = 1;";
        var status = (string)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal("SEALED", status);
    }

    [Fact]
    public async Task Sealed_WithMissingUserRecord_StaysSealed()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        var dummyUserId = Guid.NewGuid();

        await using (var setupConn = new NpgsqlConnection(_fixture.SuperuserConnectionString))
        {
            await setupConn.OpenAsync();

            // Insert synthetic user
            await using (var userCmd = setupConn.CreateCommand())
            {
                userCmd.CommandText = @"
                    INSERT INTO asp_net_users (id, user_name, normalized_user_name, password_hash, security_stamp, concurrency_stamp, lockout_enabled, access_failed_count)
                    VALUES (@id, 'test_seal_owner', 'TEST_SEAL_OWNER', 'hash', gen_random_uuid()::text, gen_random_uuid()::text, false, 0)
                    ON CONFLICT (id) DO NOTHING;
                ";
                userCmd.Parameters.AddWithValue("id", dummyUserId);
                await userCmd.ExecuteNonQueryAsync();
            }

            // Seal with user id
            await SetBootstrapStatusAsync("SEALED", dummyUserId);

            // Delete user record (FK onDelete is SetNull)
            await using (var delUserCmd = setupConn.CreateCommand())
            {
                delUserCmd.CommandText = "DELETE FROM asp_net_users WHERE id = @id;";
                delUserCmd.Parameters.AddWithValue("id", dummyUserId);
                await delUserCmd.ExecuteNonQueryAsync();
            }
        }

        // Verify status remains SEALED even when user record is gone
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/bootstrap/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("SEALED", doc.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Bootstrapping_AbandonedTransaction_RollsBackToUninitialized_OrStaysSafe()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await SetBootstrapStatusAsync("UNINITIALIZED");

        // Transaction begins transition to BOOTSTRAPPING and then rolls back
        await using (var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString))
        {
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            await using (var updateCmd = conn.CreateCommand())
            {
                updateCmd.Transaction = tx;
                updateCmd.CommandText = "UPDATE deployment_bootstrap_state SET status = 'BOOTSTRAPPING' WHERE id = 1 AND status = 'UNINITIALIZED';";
                var rows = await updateCmd.ExecuteNonQueryAsync();
                Assert.Equal(1, rows);
            }

            // Abandon / rollback
            await tx.RollbackAsync();
        }

        // Verify state is safely back to UNINITIALIZED
        await using (var verifyConn = new NpgsqlConnection(_fixture.RuntimeConnectionString))
        {
            await verifyConn.OpenAsync();
            await using var verifyCmd = verifyConn.CreateCommand();
            verifyCmd.CommandText = "SELECT status FROM deployment_bootstrap_state WHERE id = 1;";
            var status = (string)(await verifyCmd.ExecuteScalarAsync())!;
            Assert.Equal("UNINITIALIZED", status);
        }
    }
}
