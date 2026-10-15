namespace RagPipeline.IntegrationTests.Persistence;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RagPipeline.Persistence;
using RagPipeline.Persistence.Entities;
using Xunit;

[Collection("PostgresDatabase")]
public class PooledConnectionTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private Guid _tenantAId;
    private Guid _tenantBId;

    public PooledConnectionTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        _tenantAId = Guid.NewGuid();
        _tenantBId = Guid.NewGuid();

        await using var superContext = _fixture.CreateSuperuserDbContext();

        var tenantA = new Tenant
        {
            Id = _tenantAId,
            Name = "Pool Tenant A",
            Slug = $"pool-a-{_tenantAId:N}",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var tenantB = new Tenant
        {
            Id = _tenantBId,
            Name = "Pool Tenant B",
            Slug = $"pool-b-{_tenantBId:N}",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow
        };

        superContext.Tenants.AddRange(tenantA, tenantB);

        var profileA = new TenantProfile
        {
            TenantId = _tenantAId,
            DisplayName = "Pool Tenant A Profile",
            SettingsJson = "{}",
            ConcurrencyToken = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var profileB = new TenantProfile
        {
            TenantId = _tenantBId,
            DisplayName = "Pool Tenant B Profile",
            SettingsJson = "{}",
            ConcurrencyToken = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        superContext.TenantProfiles.AddRange(profileA, profileB);

        await superContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        try
        {
            await using var superContext = _fixture.CreateSuperuserDbContext();
            var profiles = await superContext.TenantProfiles.Where(p => p.TenantId == _tenantAId || p.TenantId == _tenantBId).ToListAsync();
            superContext.TenantProfiles.RemoveRange(profiles);

            var tenants = await superContext.Tenants.Where(t => t.Id == _tenantAId || t.Id == _tenantBId).ToListAsync();
            superContext.Tenants.RemoveRange(tenants);

            await superContext.SaveChangesAsync();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private string GetPoolConnectionString(int maxPoolSize = 1)
    {
        var builder = new NpgsqlConnectionStringBuilder(_fixture.RuntimeConnectionString)
        {
            Pooling = true,
            MaxPoolSize = maxPoolSize,
            MinPoolSize = 1
        };
        return builder.ConnectionString;
    }

    [Fact]
    public async Task PooledConnection_ReusedAfterCommit_HasNoTenantContext()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        var connStr = GetPoolConnectionString(maxPoolSize: 1);

        // Checkout 1: Set Tenant A in a transaction and commit
        await using (var conn1 = new NpgsqlConnection(connStr))
        {
            await conn1.OpenAsync();
            await using var tx = await conn1.BeginTransactionAsync();

            await using var cmd = conn1.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tenantId, true);";
            cmd.Parameters.AddWithValue("tenantId", _tenantAId.ToString());
            await cmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
        }

        // Checkout 2: Same physical pooled connection must have NO lingering context
        await using (var conn2 = new NpgsqlConnection(connStr))
        {
            await conn2.OpenAsync();

            await using var cmd = conn2.CreateCommand();
            cmd.CommandText = "SELECT NULLIF(current_setting('app.current_tenant_id', true), '');";
            var value = await cmd.ExecuteScalarAsync();

            Assert.True(value == null || value == DBNull.Value, "GUC must be empty or null after commit on recycled connection.");
        }
    }

    [Fact]
    public async Task PooledConnection_ReusedAfterRollback_HasNoTenantContext()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        var connStr = GetPoolConnectionString(maxPoolSize: 1);

        // Checkout 1: Set Tenant A in a transaction and rollback
        await using (var conn1 = new NpgsqlConnection(connStr))
        {
            await conn1.OpenAsync();
            await using var tx = await conn1.BeginTransactionAsync();

            await using var cmd = conn1.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tenantId, true);";
            cmd.Parameters.AddWithValue("tenantId", _tenantAId.ToString());
            await cmd.ExecuteNonQueryAsync();

            await tx.RollbackAsync();
        }

        // Checkout 2: Same physical pooled connection must have NO lingering context
        await using (var conn2 = new NpgsqlConnection(connStr))
        {
            await conn2.OpenAsync();

            await using var cmd = conn2.CreateCommand();
            cmd.CommandText = "SELECT NULLIF(current_setting('app.current_tenant_id', true), '');";
            var value = await cmd.ExecuteScalarAsync();

            Assert.True(value == null || value == DBNull.Value, "GUC must be empty or null after rollback on recycled connection.");
        }
    }

    [Fact]
    public async Task PooledConnection_ReusedAfterCancellation_HasNoTenantContext()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        var connStr = GetPoolConnectionString(maxPoolSize: 1);

        // Checkout 1: Set Tenant A and simulate cancellation
        try
        {
            await using var conn1 = new NpgsqlConnection(connStr);
            await conn1.OpenAsync();
            await using var tx = await conn1.BeginTransactionAsync();

            await using (var setCmd = conn1.CreateCommand())
            {
                setCmd.Transaction = tx;
                setCmd.CommandText = "SELECT set_config('app.current_tenant_id', @tenantId, true);";
                setCmd.Parameters.AddWithValue("tenantId", _tenantAId.ToString());
                await setCmd.ExecuteNonQueryAsync();
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await using (var sleepCmd = conn1.CreateCommand())
            {
                sleepCmd.Transaction = tx;
                sleepCmd.CommandText = "SELECT pg_sleep(5);";
                await sleepCmd.ExecuteNonQueryAsync(cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation
        }
        catch (PostgresException)
        {
            // Expected cancellation from Postgres
        }

        // Checkout 2: Recycled connection must have NO lingering context
        await using (var conn2 = new NpgsqlConnection(connStr))
        {
            await conn2.OpenAsync();

            await using var cmd = conn2.CreateCommand();
            cmd.CommandText = "SELECT NULLIF(current_setting('app.current_tenant_id', true), '');";
            var value = await cmd.ExecuteScalarAsync();

            Assert.True(value == null || value == DBNull.Value, "GUC must be empty or null after cancellation on recycled connection.");
        }
    }

    [Fact]
    public async Task PooledConnection_ReusedAfterUnhandledException_HasNoTenantContext()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        var connStr = GetPoolConnectionString(maxPoolSize: 1);

        // Checkout 1: Set Tenant A and trigger unhandled SQL exception
        try
        {
            await using var conn1 = new NpgsqlConnection(connStr);
            await conn1.OpenAsync();
            await using var tx = await conn1.BeginTransactionAsync();

            await using (var setCmd = conn1.CreateCommand())
            {
                setCmd.Transaction = tx;
                setCmd.CommandText = "SELECT set_config('app.current_tenant_id', @tenantId, true);";
                setCmd.Parameters.AddWithValue("tenantId", _tenantAId.ToString());
                await setCmd.ExecuteNonQueryAsync();
            }

            await using (var errorCmd = conn1.CreateCommand())
            {
                errorCmd.Transaction = tx;
                errorCmd.CommandText = "SELECT 1 / 0;";
                await errorCmd.ExecuteNonQueryAsync();
            }
        }
        catch (PostgresException)
        {
            // Expected division by zero error
        }

        // Checkout 2: Recycled connection must have NO lingering context
        await using (var conn2 = new NpgsqlConnection(connStr))
        {
            await conn2.OpenAsync();

            await using var cmd = conn2.CreateCommand();
            cmd.CommandText = "SELECT NULLIF(current_setting('app.current_tenant_id', true), '');";
            var value = await cmd.ExecuteScalarAsync();

            Assert.True(value == null || value == DBNull.Value, "GUC must be empty or null after unhandled exception on recycled connection.");
        }
    }

    [Fact]
    public async Task SetConfig_IsTransactionLocal()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        // 1. Begin transaction, set with is_local = true (3rd argument)
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await using (var setCmd = conn.CreateCommand())
            {
                setCmd.Transaction = tx;
                setCmd.CommandText = "SELECT set_config('app.current_tenant_id', @tenantId, true);";
                setCmd.Parameters.AddWithValue("tenantId", _tenantAId.ToString());
                await setCmd.ExecuteNonQueryAsync();
            }

            // Inside transaction: GUC is visible
            await using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.Transaction = tx;
                checkCmd.CommandText = "SELECT current_setting('app.current_tenant_id', true);";
                var value = (string?)await checkCmd.ExecuteScalarAsync();
                Assert.Equal(_tenantAId.ToString(), value);
            }

            await tx.CommitAsync();
        }

        // 2. Outside transaction on the EXACT same open connection: GUC is gone!
        await using (var postTxCmd = conn.CreateCommand())
        {
            postTxCmd.CommandText = "SELECT NULLIF(current_setting('app.current_tenant_id', true), '');";
            var value = await postTxCmd.ExecuteScalarAsync();
            Assert.True(value == null || value == DBNull.Value, "Value must be gone after transaction commit. Session SET was not used.");
        }
    }

    [Fact]
    public async Task ConcurrentTenants_100Parallel_NoCrossTenantRows()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        // Small pool of 5 connections to force heavy connection recycling and interleaved reuse
        var connStr = GetPoolConnectionString(maxPoolSize: 5);

        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            var isTenantA = i % 2 == 0;
            var assignedTenant = isTenantA ? _tenantAId : _tenantBId;
            var otherTenant = isTenantA ? _tenantBId : _tenantAId;

            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            await using var tx = await conn.BeginTransactionAsync();

            await using (var setCmd = conn.CreateCommand())
            {
                setCmd.Transaction = tx;
                setCmd.CommandText = "SELECT set_config('app.current_tenant_id', @tenantId, true);";
                setCmd.Parameters.AddWithValue("tenantId", assignedTenant.ToString());
                await setCmd.ExecuteNonQueryAsync();
            }

            // Read all visible rows from tenant_profiles
            await using (var queryCmd = conn.CreateCommand())
            {
                queryCmd.Transaction = tx;
                queryCmd.CommandText = "SELECT tenant_id FROM tenant_profiles;";
                await using var reader = await queryCmd.ExecuteReaderAsync();

                var seenTenants = new List<Guid>();
                while (await reader.ReadAsync())
                {
                    seenTenants.Add(reader.GetGuid(0));
                }

                // Must see own tenant and NEVER see other tenant
                Assert.Contains(assignedTenant, seenTenants);
                Assert.DoesNotContain(otherTenant, seenTenants);
            }

            await tx.CommitAsync();
        });

        await Task.WhenAll(tasks);
    }
}
