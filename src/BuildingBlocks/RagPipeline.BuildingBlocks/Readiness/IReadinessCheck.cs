namespace RagPipeline.BuildingBlocks.Readiness;

public interface IReadinessCheck
{
    string Name { get; }

    Task<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default);
}
