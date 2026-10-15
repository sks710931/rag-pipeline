namespace RagPipeline.BuildingBlocks.Readiness;

public sealed class DatabaseReadinessCheck : IReadinessCheck
{
    public const string DefaultName = "database";

    public string Name => DefaultName;

    public Task<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        // No DB driver yet; never connect, never echo config values
        return Task.FromResult(ReadinessResult.Unchecked());
    }
}
