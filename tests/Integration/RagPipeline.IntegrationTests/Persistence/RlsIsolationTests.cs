namespace RagPipeline.IntegrationTests.Persistence;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RagPipeline.Persistence;
using RagPipeline.Persistence.Entities;
using Xunit;

[Collection("PostgresDatabase")]
public class RlsIsolationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private Guid _tenantAId;
    private Guid _tenantBId;
    private Guid _userAId;
    private Guid _userBId;

    public RlsIsolationTests(PostgresFixture fixture)
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
        _userAId = Guid.NewGuid();
        _userBId = Guid.NewGuid();

        await using var superContext = _fixture.CreateSuperuserDbContext();

        // 1. Seed Users
        var userA = new ApplicationUser
        {
            Id = _userAId,
            UserName = $"user-a-{_userAId:N}",
            NormalizedUserName = $"USER-A-{_userAId:N}",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        var userB = new ApplicationUser
        {
            Id = _userBId,
            UserName = $"user-b-{_userBId:N}",
            NormalizedUserName = $"USER-B-{_userBId:N}",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        superContext.Users.AddRange(userA, userB);

        // 2. Seed Tenants
        var tenantA = new Tenant
        {
            Id = _tenantAId,
            Name = "Tenant A",
            Slug = $"tenant-a-{_tenantAId:N}",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var tenantB = new Tenant
        {
            Id = _tenantBId,
            Name = "Tenant B",
            Slug = $"tenant-b-{_tenantBId:N}",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow
        };

        superContext.Tenants.AddRange(tenantA, tenantB);

        // 3. Seed Memberships
        var membershipA = new TenantMembership
        {
            TenantId = _tenantAId,
            UserId = _userAId,
            Role = "TenantOwner",
            IsFirstOwner = true,
            JoinedAt = DateTimeOffset.UtcNow
        };

        var membershipB = new TenantMembership
        {
            TenantId = _tenantBId,
            UserId = _userBId,
            Role = "TenantOwner",
            IsFirstOwner = true,
            JoinedAt = DateTimeOffset.UtcNow
        };

        superContext.TenantMemberships.AddRange(membershipA, membershipB);

        // 4. Seed Profiles
        var profileA = new TenantProfile
        {
            TenantId = _tenantAId,
            DisplayName = "Tenant A Profile",
            SettingsJson = "{\"theme\":\"light\"}",
            ConcurrencyToken = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var profileB = new TenantProfile
        {
            TenantId = _tenantBId,
            DisplayName = "Tenant B Profile",
            SettingsJson = "{\"theme\":\"dark\"}",
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

            var memberships = await superContext.TenantMemberships.Where(m => m.TenantId == _tenantAId || m.TenantId == _tenantBId).ToListAsync();
            superContext.TenantMemberships.RemoveRange(memberships);

            var tenants = await superContext.Tenants.Where(t => t.Id == _tenantAId || t.Id == _tenantBId).ToListAsync();
            superContext.Tenants.RemoveRange(tenants);

            var users = await superContext.Users.Where(u => u.Id == _userAId || u.Id == _userBId).ToListAsync();
            superContext.Users.RemoveRange(users);

            await superContext.SaveChangesAsync();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Theory]
    [InlineData("tenant_profiles")]
    [InlineData("tenant_memberships")]
    public async Task Rls_TenantA_CannotSelectTenantBRows(string tableName)
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();

        // Set context to Tenant A
        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.Transaction = tx;
            setCmd.CommandText = "SELECT set_config('app.current_tenant_id', @tenantId, true);";
            setCmd.Parameters.AddWithValue("tenantId", _tenantAId.ToString());
            await setCmd.ExecuteNonQueryAsync();
        }

        // Query Tenant A rows
        await using (var selectACmd = conn.CreateCommand())
        {
            selectACmd.Transaction = tx;
            selectACmd.CommandText = $"SELECT count(*) FROM {tableName} WHERE tenant_id = @tenantId;";
            selectACmd.Parameters.AddWithValue("tenantId", _tenantAId);
            var countA = Convert.ToInt32(await selectACmd.ExecuteScalarAsync());
            Assert.True(countA >= 1, $"Tenant A must see its own row in {tableName}");
        }

        // Query Tenant B rows: RLS must hide Tenant B rows
        await using (var selectBCmd = conn.CreateCommand())
        {
            selectBCmd.Transaction = tx;
            selectBCmd.CommandText = $"SELECT count(*) FROM {tableName} WHERE tenant_id = @tenantId;";
            selectBCmd.Parameters.AddWithValue("tenantId", _tenantBId);
            var countB = Convert.ToInt32(await selectBCmd.ExecuteScalarAsync());
            Assert.Equal(0, countB);
        }

        // Query ALL rows without WHERE filter: must ONLY see Tenant A rows
        await using (var selectAllCmd = conn.CreateCommand())
        {
            selectAllCmd.Transaction = tx;
            selectAllCmd.CommandText = $"SELECT tenant_id FROM {tableName};";
            await using var reader = await selectAllCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var rowTenantId = reader.GetGuid(0);
                Assert.Equal(_tenantAId, rowTenantId);
                Assert.NotEqual(_tenantBId, rowTenantId);
            }
        }

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Rls_TenantA_CannotInsertOrUpdateRowWithTenantB()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();

        // Set context to Tenant A
        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.Transaction = tx;
            setCmd.CommandText = "SELECT set_config('app.current_tenant_id', @tenantId, true);";
            setCmd.Parameters.AddWithValue("tenantId", _tenantAId.ToString());
            await setCmd.ExecuteNonQueryAsync();
        }

        // 1. Attempt to insert a row with Tenant B's tenant_id while context is Tenant A -> Rejected by WITH CHECK
        await using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.Transaction = tx;
            insertCmd.CommandText = @"
                INSERT INTO tenant_profiles (tenant_id, display_name, settings_json, concurrency_token, created_at, updated_at)
                VALUES (@tenantId, 'Forged Profile', '{}', gen_random_uuid(), clock_timestamp(), clock_timestamp());
            ";
            insertCmd.Parameters.AddWithValue("tenantId", _tenantBId);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => insertCmd.ExecuteNonQueryAsync());
            Assert.True(ex.SqlState == "44000" || ex.SqlState == "42501");
        }

        // 2. Attempt to update Tenant A's row to change tenant_id to Tenant B -> Rejected by WITH CHECK
        await using (var updateCmd = conn.CreateCommand())
        {
            updateCmd.Transaction = tx;
            updateCmd.CommandText = "UPDATE tenant_profiles SET tenant_id = @tenantB WHERE tenant_id = @tenantA;";
            updateCmd.Parameters.AddWithValue("tenantA", _tenantAId);
            updateCmd.Parameters.AddWithValue("tenantB", _tenantBId);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => updateCmd.ExecuteNonQueryAsync());
            Assert.True(ex.SqlState == "44000" || ex.SqlState == "42501");
        }

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Rls_UnsetContext_ReturnsZeroRowsAndRejectsWrites()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();

        // Do not call set_config at all: context is unset (null)
        // 1. Selects return 0 rows
        await using (var readCmd = conn.CreateCommand())
        {
            readCmd.Transaction = tx;
            readCmd.CommandText = "SELECT count(*) FROM tenant_profiles;";
            var count = Convert.ToInt32(await readCmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }

        await using (var memberCmd = conn.CreateCommand())
        {
            memberCmd.Transaction = tx;
            memberCmd.CommandText = "SELECT count(*) FROM tenant_memberships;";
            var count = Convert.ToInt32(await memberCmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }

        // 2. Writes are rejected
        await using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.Transaction = tx;
            insertCmd.CommandText = @"
                INSERT INTO tenant_profiles (tenant_id, display_name, settings_json, concurrency_token, created_at, updated_at)
                VALUES (@tenantId, 'Unset Write', '{}', gen_random_uuid(), clock_timestamp(), clock_timestamp());
            ";
            insertCmd.Parameters.AddWithValue("tenantId", _tenantAId);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => insertCmd.ExecuteNonQueryAsync());
            Assert.True(ex.SqlState == "44000" || ex.SqlState == "42501");
        }

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Rls_EmptyStringContext_FailsClosed()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();

        // set_config to empty string ''
        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.Transaction = tx;
            setCmd.CommandText = "SELECT set_config('app.current_tenant_id', '', true);";
            await setCmd.ExecuteNonQueryAsync();
        }

        // Must fail closed (0 rows), NULLIF transforms '' to NULL, so no cast exception or data leak
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT count(*) FROM tenant_profiles;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Rls_MalformedTenantGuc_FailsClosedWithoutLeak()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();

        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.Transaction = tx;
            setCmd.CommandText = "SELECT set_config('app.current_tenant_id', 'not-a-valid-uuid', true);";
            await setCmd.ExecuteNonQueryAsync();
        }

        // When evaluating NULLIF(current_setting(...), '')::uuid on malformed text:
        // Postgres throws an invalid syntax error (SqlState 22P02), or returns 0 rows. It MUST NEVER return rows.
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT count(*) FROM tenant_profiles;";

            try
            {
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                Assert.Equal(0, count);
            }
            catch (PostgresException ex)
            {
                // 22P02 = invalid_text_representation (cast failure)
                Assert.Equal("22P02", ex.SqlState);
            }
        }

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Rls_ForcedAndEnabled_OnAllTenantTables()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();

        var tenantTables = new[] { "tenant_profiles", "tenant_memberships" };

        foreach (var tableName in tenantTables)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT relrowsecurity, relforcerowsecurity
                FROM pg_class
                WHERE relname = @tableName AND relnamespace = 'public'::regnamespace;
            ";
            cmd.Parameters.AddWithValue("tableName", tableName);

            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), $"Table {tableName} not found in pg_class");

            var relRowSecurity = reader.GetBoolean(0);
            var relForceRowSecurity = reader.GetBoolean(1);

            Assert.True(relRowSecurity, $"RLS must be enabled on {tableName} (relrowsecurity = true)");
            Assert.True(relForceRowSecurity, $"RLS must be forced on {tableName} (relforcerowsecurity = true)");
        }
    }

    [Fact]
    public async Task EfBulkPaths_ExecuteUpdateDeleteAndRawSql_RunUnderTenantContext()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        // Using EF DbContext without setting tenant transaction -> Throws before reaching the DB
        await using var context = _fixture.CreateRuntimeDbContext(withInterceptors: true);

        // 1. Raw SQL query
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Database.ExecuteSqlRawAsync("SELECT * FROM tenant_profiles"));

        // 2. ExecuteUpdateAsync
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.TenantProfiles.Where(p => p.DisplayName == "test").ExecuteUpdateAsync(s => s.SetProperty(p => p.DisplayName, "new")));

        // 3. ExecuteDeleteAsync
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.TenantProfiles.Where(p => p.DisplayName == "test").ExecuteDeleteAsync());

        // 4. SaveChanges with tenant entities
        context.TenantProfiles.Add(new TenantProfile
        {
            TenantId = Guid.NewGuid(),
            DisplayName = "Orphan Profile",
            SettingsJson = "{}",
            ConcurrencyToken = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task BackgroundPath_WithoutTenantScope_CannotReadTenantRows()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        // Bypassing application interceptors on a background path to test Postgres RLS layer directly
        await using var context = _fixture.CreateRuntimeDbContext(withInterceptors: false);

        // Raw query under runtime role without set_config
        var profiles = await context.TenantProfiles.AsNoTracking().ToListAsync();
        Assert.Empty(profiles);

        var memberships = await context.TenantMemberships.AsNoTracking().ToListAsync();
        Assert.Empty(memberships);
    }
}
