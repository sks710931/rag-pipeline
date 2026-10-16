namespace RagPipeline.Persistence;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RagPipeline.BuildingBlocks.Bootstrap;
using RagPipeline.BuildingBlocks.Configuration;
using RagPipeline.BuildingBlocks.Persistence;

public class BootstrapStateService : IBootstrapStateService
{
    private readonly RagPipelineDbContext _dbContext;
    private readonly IBootstrapStateReader _stateReader;
    private readonly IOptions<HostingOptions> _hostingOptions;

    public BootstrapStateService(
        RagPipelineDbContext dbContext,
        IBootstrapStateReader stateReader,
        IOptions<HostingOptions> hostingOptions)
    {
        _dbContext = dbContext;
        _stateReader = stateReader;
        _hostingOptions = hostingOptions;
    }

    public async Task<BootstrapStateResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var rawResult = await _stateReader.GetStateAsync(cancellationToken);
        if (rawResult.IsUnavailable)
        {
            return rawResult;
        }

        if (_hostingOptions.Value.IsSaaS)
        {
            return BootstrapStateResult.Sealed();
        }

        return rawResult;
    }

    public async Task<bool> TryBeginBootstrappingAsync(CancellationToken cancellationToken = default)
    {
        if (_hostingOptions.Value.IsSaaS)
        {
            return false;
        }

        var rowsAffected = await _dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE deployment_bootstrap_state
              SET status = 'BOOTSTRAPPING'
              WHERE id = 1 AND status = 'UNINITIALIZED';",
            cancellationToken);

        return rowsAffected == 1;
    }

    public async Task<bool> TrySealAsync(Guid? sealedByUserId = null, CancellationToken cancellationToken = default)
    {
        int rowsAffected;
        if (sealedByUserId.HasValue)
        {
            rowsAffected = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE deployment_bootstrap_state
                   SET status = 'SEALED',
                       sealed_at = clock_timestamp(),
                       sealed_by_user_id = {sealedByUserId.Value}
                   WHERE id = 1 AND status = 'BOOTSTRAPPING';",
                cancellationToken);
        }
        else
        {
            rowsAffected = await _dbContext.Database.ExecuteSqlRawAsync(
                @"UPDATE deployment_bootstrap_state
                  SET status = 'SEALED',
                      sealed_at = clock_timestamp()
                  WHERE id = 1 AND status = 'BOOTSTRAPPING';",
                cancellationToken);
        }

        return rowsAffected == 1;
    }
}
