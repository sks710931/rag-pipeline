namespace RagPipeline.IntegrationTests.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using Xunit;

[Collection("PostgresDatabase")]
public class MigrationSmokeTests
{
    private readonly PostgresFixture _fixture;

    public MigrationSmokeTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migrate_FreshDatabase_CreatesOnlyExpectedS0Tables()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        var expectedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "asp_net_users",
            "asp_net_user_tokens",
            "deployment_bootstrap_state",
            "deployment_owners",
            "tenants",
            "tenant_memberships",
            "tenant_profiles",
            "auth_sessions",
            "auth_session_revocations",
            "audit_events"
        };

        var prohibitedTokens = new[]
        {
            "document",
            "cdm",
            "element",
            "retrieval",
            "index_entry",
            "stage_lease",
            "key_protection",
            "key_version",
            "secret"
        };

        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_type = 'BASE TABLE'
              AND table_name <> '__EFMigrationsHistory'
            ORDER BY table_name;
        ";

        var actualTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                actualTables.Add(reader.GetString(0));
            }
        }

        Assert.Equal(expectedTables, actualTables);

        foreach (var table in actualTables)
        {
            foreach (var token in prohibitedTokens)
            {
                Assert.DoesNotContain(token, table, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Verify bootstrap singleton row is seeded
        await using var seedCheckCmd = conn.CreateCommand();
        seedCheckCmd.CommandText = "SELECT status FROM deployment_bootstrap_state WHERE id = 1;";
        var status = (string?)await seedCheckCmd.ExecuteScalarAsync();
        Assert.Equal("UNINITIALIZED", status);
    }

    [Fact]
    public async Task Migrate_TwiceInARow_IsIdempotent()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var context = _fixture.CreateSuperuserDbContext();
        await context.Database.MigrateAsync();

        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory';";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        Assert.Equal(10, count);
    }

    [Fact]
    public async Task Migrate_FromEachPriorMigration_ReachesSameSchema()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        // Connect to postgres and create a temporary database
        var tempDbName = $"test_migration_step_{Guid.NewGuid():N}";
        await using var adminConn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await adminConn.OpenAsync();

        await using (var createDbCmd = adminConn.CreateCommand())
        {
            createDbCmd.CommandText = $"CREATE DATABASE \"{tempDbName}\";";
            await createDbCmd.ExecuteNonQueryAsync();
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(_fixture.SuperuserConnectionString)
            {
                Database = tempDbName
            };
            var tempConnStr = builder.ConnectionString;

            var options = new DbContextOptionsBuilder<RagPipeline.Persistence.RagPipelineDbContext>()
                .UseNpgsql(tempConnStr, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
                .Options;

            await using (var stepContext = new RagPipeline.Persistence.RagPipelineDbContext(options))
            {
                // Apply first migration
                var migrator = stepContext.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
                await migrator.MigrateAsync("InitialS0Schema");

                // Verify tables exist before roles migration
                await using var conn = new NpgsqlConnection(tempConnStr);
                await conn.OpenAsync();
                await using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory';";
                var tableCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                Assert.Equal(10, tableCount);

                // Apply second migration
                await migrator.MigrateAsync("CreateRolesAndGrants");
            }

            // Final table check
            await using var finalConn = new NpgsqlConnection(tempConnStr);
            await finalConn.OpenAsync();
            await using var finalCmd = finalConn.CreateCommand();
            finalCmd.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory';";
            var finalCount = Convert.ToInt32(await finalCmd.ExecuteScalarAsync());
            Assert.Equal(10, finalCount);
        }
        finally
        {
            await using var dropCmd = adminConn.CreateCommand();
            dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{tempDbName}\" WITH (FORCE);";
            await dropCmd.ExecuteNonQueryAsync();
        }
    }
}
