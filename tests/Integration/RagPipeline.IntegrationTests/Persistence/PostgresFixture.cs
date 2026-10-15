namespace RagPipeline.IntegrationTests.Persistence;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using RagPipeline.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

public class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    public bool DockerAvailable { get; private set; }

    public string SuperuserConnectionString => _container?.GetConnectionString() ?? string.Empty;
    public string MigratorConnectionString { get; private set; } = string.Empty;
    public string RuntimeConnectionString { get; private set; } = string.Empty;
    public string ProbeConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("ragpipeline")
                .WithUsername("postgres")
                .WithPassword("postgres_password")
                .Build();

            await _container.StartAsync();
            DockerAvailable = true;

            var builder = new NpgsqlConnectionStringBuilder(SuperuserConnectionString);
            var host = builder.Host;
            var port = builder.Port;
            var db = builder.Database;

            MigratorConnectionString = $"Host={host};Port={port};Database={db};Username=migrator;Password=migrator_pass";
            RuntimeConnectionString = $"Host={host};Port={port};Database={db};Username=rag_app;Password=rag_app_pass";
            ProbeConnectionString = $"Host={host};Port={port};Database={db};Username=rag_probe;Password=rag_probe_pass";

            await using var context = CreateSuperuserDbContext();
            await context.Database.MigrateAsync();

            await using var conn = new NpgsqlConnection(SuperuserConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                ALTER ROLE migrator WITH PASSWORD 'migrator_pass';
                ALTER ROLE rag_app WITH PASSWORD 'rag_app_pass';
                ALTER ROLE rag_probe WITH PASSWORD 'rag_probe_pass';
            ";
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception)
        {
            DockerAvailable = false;
        }
    }

    public RagPipelineDbContext CreateSuperuserDbContext()
    {
        var options = new DbContextOptionsBuilder<RagPipelineDbContext>()
            .UseNpgsql(SuperuserConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .Options;
        return new RagPipelineDbContext(options);
    }

    public RagPipelineDbContext CreateMigratorDbContext()
    {
        var options = new DbContextOptionsBuilder<RagPipelineDbContext>()
            .UseNpgsql(MigratorConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .Options;
        return new RagPipelineDbContext(options);
    }

    public RagPipelineDbContext CreateRuntimeDbContext()
    {
        var options = new DbContextOptionsBuilder<RagPipelineDbContext>()
            .UseNpgsql(RuntimeConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .Options;
        return new RagPipelineDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition("PostgresDatabase")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}
