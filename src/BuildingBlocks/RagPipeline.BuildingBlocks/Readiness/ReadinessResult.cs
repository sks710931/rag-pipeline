namespace RagPipeline.BuildingBlocks.Readiness;

public sealed record ReadinessResult(ReadinessStatus Status, string? Description = null)
{
    public static ReadinessResult Ready(string? description = null) => new(ReadinessStatus.Ready, description);

    public static ReadinessResult NotReady(string? description = null) => new(ReadinessStatus.NotReady, description);

    public static ReadinessResult Unchecked(string? description = null) => new(ReadinessStatus.Unchecked, description);

    public static ReadinessResult NotConfigured(string? description = null) => new(ReadinessStatus.NotConfigured, description);
}
