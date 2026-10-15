namespace RagPipeline.Persistence.Entities;

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ActorUserId { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public Guid? DeploymentId { get; set; }
    public Guid? TenantId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetResource { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string Outcome { get; set; } = "Success";
    public string? CorrelationId { get; set; }
    public System.Net.IPAddress? ClientIp { get; set; }
    public string? DetailsJson { get; set; }
}
