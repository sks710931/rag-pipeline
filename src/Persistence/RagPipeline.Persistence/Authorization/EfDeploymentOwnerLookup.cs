namespace RagPipeline.Persistence.Authorization;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagPipeline.BuildingBlocks.Authorization;

public sealed class EfDeploymentOwnerLookup : IDeploymentOwnerLookup
{
    private readonly RagPipelineDbContext _dbContext;

    public EfDeploymentOwnerLookup(RagPipelineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<bool> IsDeploymentSuperadminAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        return await _dbContext.DeploymentOwners
            .AsNoTracking()
            .AnyAsync(o => o.UserId == userId && o.Role == Roles.DeploymentSuperadmin, cancellationToken);
    }
}
