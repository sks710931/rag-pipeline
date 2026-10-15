namespace RagPipeline.IntegrationTests.Isolation;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

[Collection("IsolationCollection")]
[Trait("Category", "Isolation")]
public class IsolationPrivilegeTests
{
    private readonly IsolationFixture _fixture;

    public IsolationPrivilegeTests(IsolationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Runtime_CannotCreateAlterDropTruncate()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        // 1. CREATE TABLE
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE malicious_isolation (id int);";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 2. ALTER TABLE
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "ALTER TABLE tenants ADD COLUMN malicious_col text;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. DROP TABLE
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DROP TABLE tenant_profiles;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 4. TRUNCATE TABLE
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "TRUNCATE TABLE tenant_profiles;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 5. CREATE SCHEMA
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE SCHEMA malicious_iso_schema;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
    }

    [Fact]
    public async Task Runtime_CannotSetRoleToMigratorOrSuperuser()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        // 1. SET ROLE migrator
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SET ROLE migrator;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 2. SET ROLE postgres
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SET ROLE postgres;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
    }

    [Fact]
    public async Task Runtime_CannotDisableRlsOrAlterPolicy()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        // 1. DISABLE ROW LEVEL SECURITY
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "ALTER TABLE tenant_profiles DISABLE ROW LEVEL SECURITY;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 2. NO FORCE ROW LEVEL SECURITY
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "ALTER TABLE tenant_profiles NO FORCE ROW LEVEL SECURITY;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. DROP POLICY
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DROP POLICY tenant_isolation_policy ON tenant_profiles;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 4. CREATE POLICY
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE POLICY bypass_policy ON tenant_profiles USING (true);";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
    }

    [Fact]
    public async Task Runtime_CannotChangeSessionAuthorization()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        // 1. SET SESSION AUTHORIZATION migrator
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SET SESSION AUTHORIZATION migrator;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 2. SET SESSION AUTHORIZATION postgres
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SET SESSION AUTHORIZATION postgres;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
    }

    [Fact]
    public async Task Runtime_CannotReadOrWritePgCatalogSensitive()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        // 1. SELECT from pg_authid
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM pg_authid;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 2. SELECT from pg_shadow
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM pg_shadow;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. UPDATE catalog
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE pg_database SET datname = 'malicious' WHERE datname = 'ragpipeline';";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
    }

    [Fact]
    public async Task ProbeRole_HasNoAccessToTenantOrIdentityTables()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var conn = new NpgsqlConnection(_fixture.ProbeConnectionString);
        await conn.OpenAsync();

        // Prohibited tables for probe role
        var prohibitedTables = new[]
        {
            "tenants",
            "tenant_profiles",
            "tenant_memberships",
            "asp_net_users",
            "audit_events",
            "auth_sessions"
        };

        foreach (var table in prohibitedTables)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT count(*) FROM {table};";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // Permitted read-only tables for probe role
        await using (var okCmd1 = conn.CreateCommand())
        {
            okCmd1.CommandText = "SELECT count(*) FROM deployment_bootstrap_state;";
            var count = await okCmd1.ExecuteScalarAsync();
            Assert.NotNull(count);
        }

        await using (var okCmd2 = conn.CreateCommand())
        {
            okCmd2.CommandText = "SELECT count(*) FROM \"__EFMigrationsHistory\";";
            var count = await okCmd2.ExecuteScalarAsync();
            Assert.NotNull(count);
        }
    }

    [Fact]
    public async Task Migrate_RunUnderMigratorOnly_RuntimeRoleFails()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        // Attempting to migrate under runtime role fails DDL
        await using (var runtimeContext = _fixture.CreateRuntimeDbContext())
        {
            await Assert.ThrowsAnyAsync<Exception>(() => runtimeContext.Database.MigrateAsync());
        }

        // Migrator role succeeds
        await using (var migratorContext = _fixture.CreateMigratorDbContext())
        {
            await migratorContext.Database.MigrateAsync();
        }
    }

    [Fact]
    public async Task Migrate_Rerun_IsIdempotent()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var migratorContext = _fixture.CreateMigratorDbContext();

        // Re-running migrations on the already up-to-date database must succeed idempotently
        await migratorContext.Database.MigrateAsync();
    }

    [Fact]
    public async Task Install_Rerun_OnExistingDb_NoDataLoss()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var superContext = _fixture.CreateSuperuserDbContext();

        // 1. Record existing row counts
        var tenantsBefore = await superContext.Tenants.CountAsync();
        var profilesBefore = await superContext.TenantProfiles.CountAsync();
        var membershipsBefore = await superContext.TenantMemberships.CountAsync();
        var auditBefore = await superContext.AuditEvents.CountAsync();

        Assert.True(tenantsBefore >= 2);
        Assert.True(profilesBefore >= 2);
        Assert.True(membershipsBefore >= 4);
        Assert.True(auditBefore >= 2);

        // 2. Re-run migration pipeline via migrator context
        await using (var migratorContext = _fixture.CreateMigratorDbContext())
        {
            await migratorContext.Database.MigrateAsync();
        }

        // 3. Verify zero data loss: exact same counts
        var tenantsAfter = await superContext.Tenants.CountAsync();
        var profilesAfter = await superContext.TenantProfiles.CountAsync();
        var membershipsAfter = await superContext.TenantMemberships.CountAsync();
        var auditAfter = await superContext.AuditEvents.CountAsync();

        Assert.Equal(tenantsBefore, tenantsAfter);
        Assert.Equal(profilesBefore, profilesAfter);
        Assert.Equal(membershipsBefore, membershipsAfter);
        Assert.Equal(auditBefore, auditAfter);
    }
}
