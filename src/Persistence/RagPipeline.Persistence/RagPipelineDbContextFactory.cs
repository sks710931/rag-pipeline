namespace RagPipeline.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class RagPipelineDbContextFactory : IDesignTimeDbContextFactory<RagPipelineDbContext>
{
    public RagPipelineDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("RAGPIPELINE_MIGRATOR_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Host=localhost;Database=ragpipeline;Username=migrator;Password=dummy";
        }

        var optionsBuilder = new DbContextOptionsBuilder<RagPipelineDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
        });

        return new RagPipelineDbContext(optionsBuilder.Options);
    }
}
