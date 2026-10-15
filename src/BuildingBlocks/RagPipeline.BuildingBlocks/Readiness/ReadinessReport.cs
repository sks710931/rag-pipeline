namespace RagPipeline.BuildingBlocks.Readiness;

using System.Text.Json.Serialization;

public sealed record ReadinessReport(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("dependencies")] IReadOnlyDictionary<string, string> Dependencies)
{
    [JsonIgnore]
    public bool IsReady => string.Equals(Status, "ready", StringComparison.OrdinalIgnoreCase);
}
