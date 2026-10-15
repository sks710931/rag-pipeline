namespace RagPipeline.Persistence;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using RagPipeline.BuildingBlocks.Persistence;
using RagPipeline.Persistence.Entities;

public class BootstrapStateReader : IBootstrapStateReader
{
    private readonly RagPipelineDbContext _dbContext;

    public BootstrapStateReader(RagPipelineDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BootstrapStateResult> GetStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return BootstrapStateResult.Unavailable("DATABASE_UNAVAILABLE");
            }

            var appliedMigrations = (await _dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            if (appliedMigrations.Count == 0)
            {
                return BootstrapStateResult.Unavailable("DATABASE_NOT_MIGRATED");
            }

            var pendingMigrations = (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pendingMigrations.Count > 0)
            {
                return BootstrapStateResult.Unavailable("DATABASE_MIGRATION_MISMATCH");
            }

            var state = await _dbContext.DeploymentBootstrapStates
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == DeploymentBootstrapState.SingletonId, cancellationToken);

            if (state == null || string.Equals(state.Status, "UNINITIALIZED", StringComparison.OrdinalIgnoreCase))
            {
                return BootstrapStateResult.Uninitialized();
            }

            if (string.Equals(state.Status, "SEALED", StringComparison.OrdinalIgnoreCase))
            {
                return BootstrapStateResult.Sealed();
            }

            return BootstrapStateResult.Unavailable($"UNKNOWN_BOOTSTRAP_STATE: {state.Status}");
        }
        catch (PostgresException ex) when (ex.SqlState == "42501")
        {
            return BootstrapStateResult.Unavailable("PERMISSION_DENIED");
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return BootstrapStateResult.Unavailable("DATABASE_NOT_MIGRATED");
        }
        catch (Exception)
        {
            return BootstrapStateResult.Unavailable("DATABASE_UNAVAILABLE");
        }
    }
}
