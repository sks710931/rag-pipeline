namespace RagPipeline.BuildingBlocks.Persistence;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IBootstrapStateService
{
    Task<BootstrapStateResult> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<bool> TryBeginBootstrappingAsync(CancellationToken cancellationToken = default);

    Task<bool> TrySealAsync(Guid? sealedByUserId = null, CancellationToken cancellationToken = default);
}
