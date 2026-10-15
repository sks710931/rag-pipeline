namespace RagPipeline.IntegrationTests.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RagPipeline.Persistence;
using Xunit;

[Collection("PostgresDatabase")]
public class DatabaseFailureSemanticsTests
{
    private readonly PostgresFixture _fixture;

    public DatabaseFailureSemanticsTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SchemaCheck_MissingMigrationHistory_ReportsUnavailableNotUninitialized()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        var tempDb = $"test_no_migrations_{Guid.NewGuid():N}";
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

            await using var context = new RagPipelineDbContext(options);
            var reader = new BootstrapStateReader(context);

            var result = await reader.GetStateAsync();

            Assert.True(result.IsUnavailable, "Empty DB with no migration history must report Unavailable.");
            Assert.False(result.IsUninitialized, "Empty DB must NEVER masquerade as Uninitialized.");
            Assert.Equal("DATABASE_NOT_MIGRATED", result.ReasonCode);
        }
        finally
        {
            await using var dropCmd = adminConn.CreateCommand();
            dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{tempDb}\" WITH (FORCE);";
            await dropCmd.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task SchemaCheck_MigrationVersionMismatch_ReportsUnavailable()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        var tempDb = $"test_migration_mismatch_{Guid.NewGuid():N}";
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
                // Apply only migration 1
                var migrator = context.GetService<IMigrator>();
                await migrator.MigrateAsync("InitialS0Schema");
            }

            await using (var context = new RagPipelineDbContext(options))
            {
                var reader = new BootstrapStateReader(context);
                var result = await reader.GetStateAsync();

                Assert.True(result.IsUnavailable, "Pending unapplied migrations must report Unavailable.");
                Assert.False(result.IsUninitialized, "Migration mismatch must NEVER masquerade as Uninitialized.");
                Assert.Equal("DATABASE_MIGRATION_MISMATCH", result.ReasonCode);
            }
        }
        finally
        {
            await using var dropCmd = adminConn.CreateCommand();
            dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{tempDb}\" WITH (FORCE);";
            await dropCmd.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task SchemaCheck_DatabaseDown_ReportsUnavailable()
    {
        // Point to an unreachable port (e.g. 59999)
        var options = new DbContextOptionsBuilder<RagPipelineDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=fake;Username=fake;Password=fake;Timeout=2;", o => o.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .Options;

        await using var context = new RagPipelineDbContext(options);
        var reader = new BootstrapStateReader(context);

        var result = await reader.GetStateAsync();

        Assert.True(result.IsUnavailable, "Unreachable database must report Unavailable.");
        Assert.False(result.IsUninitialized, "Offline database must NEVER masquerade as Uninitialized.");
        Assert.Equal("DATABASE_UNAVAILABLE", result.ReasonCode);
    }

    [Fact]
    public async Task SchemaCheck_PermissionDeniedOnBootstrapTable_ReportsUnavailable()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        // Create a restricted user without SELECT on deployment_bootstrap_state
        var restrictedUser = $"restricted_{Guid.NewGuid():N}";
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

            var options = new DbContextOptionsBuilder<RagPipelineDbContext>()
                .UseNpgsql(builder.ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
                .Options;

            await using var context = new RagPipelineDbContext(options);
            var reader = new BootstrapStateReader(context);

            var result = await reader.GetStateAsync();

            Assert.True(result.IsUnavailable, "Permission denied must report Unavailable.");
            Assert.False(result.IsUninitialized, "Permission denied must NEVER masquerade as Uninitialized.");
            Assert.Equal("PERMISSION_DENIED", result.ReasonCode);
        }
        finally
        {
            await using var dropCmd = adminConn.CreateCommand();
            dropCmd.CommandText = $"DROP ROLE IF EXISTS \"{restrictedUser}\";";
            await dropCmd.ExecuteNonQueryAsync();
        }
    }
}
