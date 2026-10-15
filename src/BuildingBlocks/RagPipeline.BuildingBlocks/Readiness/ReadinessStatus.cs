namespace RagPipeline.BuildingBlocks.Readiness;

public enum ReadinessStatus
{
    Ready,
    NotReady,
    Unchecked,
    NotConfigured
}

public static class ReadinessStatusExtensions
{
    public static string ToReportString(this ReadinessStatus status) => status switch
    {
        ReadinessStatus.Ready => "ready",
        ReadinessStatus.NotReady => "not_ready",
        ReadinessStatus.Unchecked => "unchecked",
        ReadinessStatus.NotConfigured => "not_configured",
        _ => "not_ready"
    };
}
