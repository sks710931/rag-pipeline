namespace RagPipeline.BuildingBlocks.Persistence;

public interface IBootstrapStateReader
{
    Task<BootstrapStateResult> GetStateAsync(CancellationToken cancellationToken = default);
}
