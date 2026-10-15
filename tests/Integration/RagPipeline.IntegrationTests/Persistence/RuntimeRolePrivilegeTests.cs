namespace RagPipeline.IntegrationTests.Persistence;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

[Collection("PostgresDatabase")]
public class RuntimeRolePrivilegeTests
{
    private readonly PostgresFixture _fixture;

    public RuntimeRolePrivilegeTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RuntimeRole_CannotCreateAlterOrDropTables()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        // 1. CREATE TABLE
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE malicious_table (id int);";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 2. ALTER TABLE
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "ALTER TABLE tenants ADD COLUMN extra_col text;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. DROP TABLE
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DROP TABLE tenants;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 4. CREATE SCHEMA
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE SCHEMA malicious_schema;";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
    }

    [Fact]
    public async Task RuntimeRole_IsNotOwnerNotSuperuserNoBypassRls()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();

        // Check role privileges in pg_roles
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT rolsuper, rolbypassrls FROM pg_roles WHERE rolname = 'rag_app';";
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            var isSuperuser = reader.GetBoolean(0);
            var bypassRls = reader.GetBoolean(1);

            Assert.False(isSuperuser, "rag_app must not be a superuser.");
            Assert.False(bypassRls, "rag_app must have NOBYPASSRLS.");
        }

        // Check that rag_app owns no tables in schema public
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tableowner = 'rag_app';";
            var ownedTablesCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, ownedTablesCount);
        }
    }

    [Fact]
    public async Task RuntimeRole_CannotRunMigrations()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var runtimeContext = _fixture.CreateRuntimeDbContext();

        // Attempting to run migrations under runtime role fails DDL
        await Assert.ThrowsAnyAsync<Exception>(() => runtimeContext.Database.MigrateAsync());
    }

    [Fact]
    public async Task Migrator_ConnectionString_NotUsedByApiHost()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        // Connect as runtime role and verify current_user
        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT current_user;";
        var currentUser = (string?)await cmd.ExecuteScalarAsync();

        Assert.Equal("rag_app", currentUser);
        Assert.NotEqual("postgres", currentUser);
        Assert.NotEqual("migrator", currentUser);
    }
}
