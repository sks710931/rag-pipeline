namespace RagPipeline.BuildingBlocks.Authorization;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IDeploymentOwnerLookup
{
    Task<bool> IsDeploymentSuperadminAsync(Guid userId, CancellationToken cancellationToken = default);
}
