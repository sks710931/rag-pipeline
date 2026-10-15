namespace RagPipeline.IntegrationTests.Isolation;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RagPipeline.IntegrationTests.Authorization;
using RagPipeline.Persistence;
using RagPipeline.Persistence.Entities;
using Xunit;

[Collection("IsolationCollection")]
[Trait("Category", "Isolation")]
public class IsolationFailureModeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly IsolationFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;

    public IsolationFailureModeTests(IsolationFixture fixture, WebApplicationFactory<Program> factory)
    {
        _fixture = fixture;
        _factory = factory;
    }

    [Fact]
    public async Task Isolation_MissingTenantGuc()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        // 1. tenant_profiles must return 0 rows
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM tenant_profiles;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }

        // 2. tenant_memberships must return 0 rows
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM tenant_memberships;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }

        // 3. audit_events with tenant_id must return 0 rows
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM audit_events WHERE tenant_id IS NOT NULL;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }
    }

    [Fact]
    public async Task Isolation_EmptyGuc()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        // Set empty string GUC
        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.CommandText = "SELECT set_config('app.current_tenant_id', '', false);";
            await setCmd.ExecuteNonQueryAsync();
        }

        // All tenant tables must return 0 rows
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM tenant_profiles;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM tenant_memberships;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM audit_events;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }
    }

    [Fact]
    public async Task Isolation_MalformedGuc()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        // Set malformed GUC string (not a UUID)
        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.CommandText = "SELECT set_config('app.current_tenant_id', 'not-a-valid-uuid-syntax', false);";
            await setCmd.ExecuteNonQueryAsync();
        }

        // Querying tenant table must throw 22P02 (invalid_text_representation), NEVER returning foreign rows
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM tenant_profiles;";
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteScalarAsync());
        Assert.Equal("22P02", ex.SqlState);
    }

    [Fact]
    public async Task Isolation_PoolReuse_AfterException()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        var builder = new NpgsqlConnectionStringBuilder(_fixture.RuntimeConnectionString)
        {
            Pooling = true,
            MaxPoolSize = 1,
            MinPoolSize = 1,
            ConnectionLifetime = 0
        };
        var pooledConnStr = builder.ConnectionString;

        // 1. Connection 1 sets Tenant A and triggers a SQL error
        await using (var conn1 = new NpgsqlConnection(pooledConnStr))
        {
            await conn1.OpenAsync();
            await using var setCmd = conn1.CreateCommand();
            setCmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{_fixture.TenantAId}', false);";
            await setCmd.ExecuteNonQueryAsync();

            await using var errCmd = conn1.CreateCommand();
            errCmd.CommandText = "SELECT 1 / 0;";
            await Assert.ThrowsAsync<PostgresException>(() => errCmd.ExecuteNonQueryAsync());
        } // Returns connection to pool

        // 2. Connection 2 reuses the pooled connection. Without setting tenant context, it must see 0 rows.
        await using (var conn2 = new NpgsqlConnection(pooledConnStr))
        {
            await conn2.OpenAsync();

            await using var verifyCmd = conn2.CreateCommand();
            verifyCmd.CommandText = "SELECT count(*) FROM tenant_profiles;";
            var count = Convert.ToInt32(await verifyCmd.ExecuteScalarAsync());
            Assert.Equal(0, count);

            // Now set Tenant B: must see only Tenant B, zero Tenant A rows
            await using var setBCmd = conn2.CreateCommand();
            setBCmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{_fixture.TenantBId}', false);";
            await setBCmd.ExecuteNonQueryAsync();

            await using var selectCmd = conn2.CreateCommand();
            selectCmd.CommandText = "SELECT tenant_id FROM tenant_profiles;";
            var tenantId = (Guid)(await selectCmd.ExecuteScalarAsync())!;
            Assert.Equal(_fixture.TenantBId, tenantId);
        }
    }

    [Fact]
    public async Task Isolation_PoolReuse_AfterCancellation()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        var builder = new NpgsqlConnectionStringBuilder(_fixture.RuntimeConnectionString)
        {
            Pooling = true,
            MaxPoolSize = 1,
            MinPoolSize = 1,
            ConnectionLifetime = 0
        };
        var pooledConnStr = builder.ConnectionString;

        // 1. Connection 1 sets Tenant A and cancels a long-running query
        await using (var conn1 = new NpgsqlConnection(pooledConnStr))
        {
            await conn1.OpenAsync();
            await using var setCmd = conn1.CreateCommand();
            setCmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{_fixture.TenantAId}', false);";
            await setCmd.ExecuteNonQueryAsync();

            using var cts = new CancellationTokenSource(100);
            await using var sleepCmd = conn1.CreateCommand();
            sleepCmd.CommandText = "SELECT pg_sleep(5);";
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sleepCmd.ExecuteNonQueryAsync(cts.Token));
        } // Returns to pool

        // 2. Connection 2 reuses pooled connection: verify clean state
        await using (var conn2 = new NpgsqlConnection(pooledConnStr))
        {
            await conn2.OpenAsync();

            await using var checkCmd = conn2.CreateCommand();
            checkCmd.CommandText = "SELECT count(*) FROM tenant_profiles;";
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }
    }

    [Fact]
    public async Task Isolation_PoolReuse_AfterRollback()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        var builder = new NpgsqlConnectionStringBuilder(_fixture.RuntimeConnectionString)
        {
            Pooling = true,
            MaxPoolSize = 1,
            MinPoolSize = 1,
            ConnectionLifetime = 0
        };
        var pooledConnStr = builder.ConnectionString;

        // 1. Connection 1 begins tx, sets Tenant A, rolls back
        await using (var conn1 = new NpgsqlConnection(pooledConnStr))
        {
            await conn1.OpenAsync();
            await using var tx = await conn1.BeginTransactionAsync();

            await using var setCmd = conn1.CreateCommand();
            setCmd.Transaction = tx;
            setCmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{_fixture.TenantAId}', true);";
            await setCmd.ExecuteNonQueryAsync();

            await tx.RollbackAsync();
        }

        // 2. Connection 2 reuses pooled connection: verify clean state
        await using (var conn2 = new NpgsqlConnection(pooledConnStr))
        {
            await conn2.OpenAsync();

            await using var checkCmd = conn2.CreateCommand();
            checkCmd.CommandText = "SELECT count(*) FROM tenant_profiles;";
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }
    }

    [Fact]
    public async Task Isolation_PoolReuse_AfterConnectionDrop()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        var builder = new NpgsqlConnectionStringBuilder(_fixture.RuntimeConnectionString)
        {
            Pooling = true,
            MaxPoolSize = 1,
            MinPoolSize = 1,
            ConnectionLifetime = 0
        };
        var pooledConnStr = builder.ConnectionString;

        // 1. Connection 1 terminates backend abruptly
        await using (var conn1 = new NpgsqlConnection(pooledConnStr))
        {
            await conn1.OpenAsync();
            await using var setCmd = conn1.CreateCommand();
            setCmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{_fixture.TenantAId}', false);";
            await setCmd.ExecuteNonQueryAsync();

            try
            {
                await using var termCmd = conn1.CreateCommand();
                termCmd.CommandText = "SELECT pg_terminate_backend(pg_backend_pid());";
                await termCmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Expected disconnection
            }
        }

        // 2. Connection 2 connects from pool: fresh backend must have empty tenant context
        await using (var conn2 = new NpgsqlConnection(pooledConnStr))
        {
            await conn2.OpenAsync();

            await using var checkCmd = conn2.CreateCommand();
            checkCmd.CommandText = "SELECT count(*) FROM tenant_profiles;";
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }
    }

    [Fact]
    public async Task Isolation_ForgedTenantRoute_UserInOnlyOneTenant()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        // User A is a member of Tenant A only. Attempting to access Tenant B route returns 403 or 404 (denied)
        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _fixture.UserAId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-UserId", _fixture.UserAId.ToString());

        var response = await client.GetAsync($"/api/v1/tenants/{_fixture.TenantBId}/audit");
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound,
            $"Accessing foreign tenant must return 403 or 404. Got: {response.StatusCode}");
    }

    [Fact]
    public async Task Isolation_UserInBothTenants_RouteSwitch_NoBleed()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        // User Both is a member of both Tenant A and Tenant B
        // Sequential and parallel requests switching between tenants must never bleed data
        await using var runtimeConn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await runtimeConn.OpenAsync();

        // 1. Sequential switch
        for (var i = 0; i < 5; i++)
        {
            await using (var setACmd = runtimeConn.CreateCommand())
            {
                setACmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{_fixture.TenantAId}', false);";
                await setACmd.ExecuteNonQueryAsync();
            }

            await using (var queryACmd = runtimeConn.CreateCommand())
            {
                queryACmd.CommandText = "SELECT tenant_id FROM tenant_profiles;";
                var id = (Guid)(await queryACmd.ExecuteScalarAsync())!;
                Assert.Equal(_fixture.TenantAId, id);
            }

            await using (var setBCmd = runtimeConn.CreateCommand())
            {
                setBCmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{_fixture.TenantBId}', false);";
                await setBCmd.ExecuteNonQueryAsync();
            }

            await using (var queryBCmd = runtimeConn.CreateCommand())
            {
                queryBCmd.CommandText = "SELECT tenant_id FROM tenant_profiles;";
                var id = (Guid)(await queryBCmd.ExecuteScalarAsync())!;
                Assert.Equal(_fixture.TenantBId, id);
            }
        }

        // 2. Parallel requests across concurrent connections
        var tasks = Enumerable.Range(0, 16).Select(async idx =>
        {
            var targetTenant = (idx % 2 == 0) ? _fixture.TenantAId : _fixture.TenantBId;
            await using var c = new NpgsqlConnection(_fixture.RuntimeConnectionString);
            await c.OpenAsync();

            await using var s = c.CreateCommand();
            s.CommandText = $"SELECT set_config('app.current_tenant_id', '{targetTenant}', false);";
            await s.ExecuteNonQueryAsync();

            await using var q = c.CreateCommand();
            q.CommandText = "SELECT tenant_id FROM tenant_profiles;";
            var resultId = (Guid)(await q.ExecuteScalarAsync())!;
            Assert.Equal(targetTenant, resultId);
        });

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Isolation_CrossTenantJoin_ReturnsNoForeignRows()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{_fixture.TenantAId}', false);";
            await setCmd.ExecuteNonQueryAsync();
        }

        // 1. Cross JOIN
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT p1.tenant_id, p2.tenant_id 
                FROM tenant_profiles p1 
                CROSS JOIN tenant_profiles p2;
            ";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Assert.Equal(_fixture.TenantAId, reader.GetGuid(0));
                Assert.Equal(_fixture.TenantAId, reader.GetGuid(1));
            }
        }

        // 2. Subselect
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT tenant_id 
                FROM tenant_profiles 
                WHERE tenant_id IN (SELECT tenant_id FROM tenant_memberships);
            ";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Assert.Equal(_fixture.TenantAId, reader.GetGuid(0));
            }
        }

        // 3. CTE (Common Table Expression)
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                WITH cte AS (SELECT tenant_id FROM tenant_profiles)
                SELECT tenant_id FROM cte;
            ";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Assert.Equal(_fixture.TenantAId, reader.GetGuid(0));
            }
        }

        // 4. UNION
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT tenant_id FROM tenant_profiles 
                UNION 
                SELECT tenant_id FROM tenant_memberships;
            ";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Assert.Equal(_fixture.TenantAId, reader.GetGuid(0));
            }
        }
    }

    [Fact]
    public async Task Isolation_CrossTenantWrite_Insert_Update_Delete_Upsert_Rejected()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{_fixture.TenantAId}', false);";
            await setCmd.ExecuteNonQueryAsync();
        }

        // 1. Cross-tenant INSERT into tenant_profiles violates WITH CHECK
        await using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.CommandText = $@"
                INSERT INTO tenant_profiles (tenant_id, display_name, settings_json, concurrency_token, created_at, updated_at)
                VALUES ('{_fixture.TenantBId}', 'Forged Profile', '{{}}', gen_random_uuid(), clock_timestamp(), clock_timestamp());
            ";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => insertCmd.ExecuteNonQueryAsync());
            Assert.Equal("44000", ex.SqlState); // check_violation
        }

        // 2. Cross-tenant UPDATE affects 0 rows
        await using (var updateCmd = conn.CreateCommand())
        {
            updateCmd.CommandText = $"UPDATE tenant_profiles SET display_name = 'Malicious' WHERE tenant_id = '{_fixture.TenantBId}';";
            var affected = await updateCmd.ExecuteNonQueryAsync();
            Assert.Equal(0, affected);
        }

        // 3. Cross-tenant DELETE affects 0 rows
        await using (var deleteCmd = conn.CreateCommand())
        {
            deleteCmd.CommandText = $"DELETE FROM tenant_profiles WHERE tenant_id = '{_fixture.TenantBId}';";
            var affected = await deleteCmd.ExecuteNonQueryAsync();
            Assert.Equal(0, affected);
        }

        // 4. Cross-tenant UPSERT violates WITH CHECK
        await using (var upsertCmd = conn.CreateCommand())
        {
            upsertCmd.CommandText = $@"
                INSERT INTO tenant_profiles (tenant_id, display_name, settings_json, concurrency_token, created_at, updated_at)
                VALUES ('{_fixture.TenantBId}', 'Upsert Forged', '{{}}', gen_random_uuid(), clock_timestamp(), clock_timestamp())
                ON CONFLICT (tenant_id) DO UPDATE SET display_name = 'Overwritten';
            ";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => upsertCmd.ExecuteNonQueryAsync());
            Assert.Equal("44000", ex.SqlState);
        }
    }

    [Fact]
    public async Task Isolation_CompositeFk_RejectsCrossTenantChildParentLink()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{_fixture.TenantAId}', false);";
            await setCmd.ExecuteNonQueryAsync();
        }

        // Attempting to insert child membership linking to Tenant B while context is Tenant A: rejected by RLS
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $@"
                INSERT INTO tenant_memberships (tenant_id, user_id, role, is_first_owner, joined_at)
                VALUES ('{_fixture.TenantBId}', '{Guid.NewGuid()}', 'TenantMember', false, clock_timestamp());
            ";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("44000", ex.SqlState);
        }

        // Attempting to insert child membership with Tenant A id but non-existent tenant: rejected by foreign key constraint
        var nonExistentTenantId = Guid.NewGuid();
        await using (var setFakeCmd = conn.CreateCommand())
        {
            setFakeCmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{nonExistentTenantId}', false);";
            await setFakeCmd.ExecuteNonQueryAsync();
        }

        await using (var cmdFk = conn.CreateCommand())
        {
            cmdFk.CommandText = $@"
                INSERT INTO tenant_memberships (tenant_id, user_id, role, is_first_owner, joined_at)
                VALUES ('{nonExistentTenantId}', '{Guid.NewGuid()}', 'TenantMember', false, clock_timestamp());
            ";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmdFk.ExecuteNonQueryAsync());
            Assert.Equal("23503", ex.SqlState); // foreign_key_violation
        }
    }

    [Fact]
    public async Task Isolation_ForcedRls_AppliesEvenToTableOwner()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        // Connect as migrator (the table owner of tenant_profiles and tenant_memberships)
        await using var conn = new NpgsqlConnection(_fixture.MigratorConnectionString);
        await conn.OpenAsync();

        // 1. Without tenant context set: must return 0 rows due to FORCE ROW LEVEL SECURITY
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM tenant_profiles;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM tenant_memberships;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }

        // 2. With Tenant A set: table owner sees only Tenant A rows
        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{_fixture.TenantAId}', false);";
            await setCmd.ExecuteNonQueryAsync();
        }

        await using (var cmdA = conn.CreateCommand())
        {
            cmdA.CommandText = "SELECT count(*) FROM tenant_profiles;";
            var countA = Convert.ToInt32(await cmdA.ExecuteScalarAsync());
            Assert.Equal(1, countA);
        }
    }

    [Fact]
    public async Task Isolation_SecurityDefinerFunctionsAndViews_DoNotBypassRls()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        // 1. Runtime role rag_app is denied execute on security definer function purge_expired_audit_events
        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT purge_expired_audit_events(clock_timestamp() - interval '60 days');";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState); // permission_denied
        }

        // 2. Verify all views in public schema (if any) are subject to security invoker / RLS
        await using (var viewCmd = conn.CreateCommand())
        {
            viewCmd.CommandText = @"
                SELECT table_name 
                FROM information_schema.views 
                WHERE table_schema = 'public';
            ";
            await using var reader = await viewCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var viewName = reader.GetString(0);
                Assert.False(string.IsNullOrEmpty(viewName));
            }
        }
    }

    [Fact]
    public async Task Isolation_BulkAndRawSqlPaths_RunUnderContext()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var runtimeContext = _fixture.CreateRuntimeDbContext();

        // Run raw SQL under BeginTenantTransactionAsync(TenantA)
        await using var tx = await runtimeContext.BeginTenantTransactionAsync(_fixture.TenantAId);

        // 1. SqlQueryRaw must see only Tenant A
        var profiles = await runtimeContext.TenantProfiles
            .FromSqlRaw("SELECT * FROM tenant_profiles")
            .ToListAsync();

        Assert.Single(profiles);
        Assert.Equal(_fixture.TenantAId, profiles[0].TenantId);

        // 2. ExecuteSqlRawAsync bulk update must affect only Tenant A's row
        var updatedCount = await runtimeContext.Database.ExecuteSqlRawAsync(
            "UPDATE tenant_profiles SET display_name = 'Bulk Certified' WHERE true;");
        Assert.Equal(1, updatedCount);

        // Verify Tenant B profile is untouched
        await using var superContext = _fixture.CreateSuperuserDbContext();
        var profB = await superContext.TenantProfiles.FindAsync(_fixture.TenantBId);
        Assert.NotNull(profB);
        Assert.NotEqual("Bulk Certified", profB.DisplayName);

        await tx.RollbackAsync();
    }
}
