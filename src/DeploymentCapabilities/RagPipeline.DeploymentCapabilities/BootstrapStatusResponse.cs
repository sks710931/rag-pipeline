namespace RagPipeline.DeploymentCapabilities;

using System.Text.Json.Serialization;

public sealed record BootstrapStatusResponse([property: JsonPropertyName("state")] string State);
