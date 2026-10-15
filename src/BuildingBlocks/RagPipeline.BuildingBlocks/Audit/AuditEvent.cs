namespace RagPipeline.BuildingBlocks.Audit;

using System;
using System.Net;

public sealed record AuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public Guid? ActorUserId { get; init; }
    public string ActorType { get; init; } = string.Empty;
    public Guid? DeploymentId { get; init; }
    public Guid? TenantId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string TargetResource { get; init; } = string.Empty;
    public string? TargetId { get; init; }
    public string Outcome { get; init; } = "Success";
    public string CorrelationId { get; init; } = string.Empty;
    public IPAddress? ClientIp { get; init; }
    public string? DetailsJson { get; init; }

    public bool IsTenantScoped => TenantId.HasValue;
    public bool IsDeploymentScoped => !TenantId.HasValue;

    public static AuditEvent Create(
        string actorType,
        string action,
        string outcome,
        string correlationId,
        DateTimeOffset occurredAt,
        Guid? actorUserId = null,
        Guid? deploymentId = null,
        Guid? tenantId = null,
        string targetResource = "System",
        string? targetId = null,
        IPAddress? clientIp = null,
        string? detailsJson = null)
    {
        if (string.IsNullOrWhiteSpace(actorType))
        {
            throw new ArgumentException("ActorType cannot be null or empty.", nameof(actorType));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action cannot be null or empty.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(outcome))
        {
            throw new ArgumentException("Outcome cannot be null or empty.", nameof(outcome));
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("CorrelationId cannot be null or empty.", nameof(correlationId));
        }

        if (occurredAt == default)
        {
            throw new ArgumentException("OccurredAt must be a valid, non-default timestamp.", nameof(occurredAt));
        }

        var sanitizedDetails = detailsJson != null ? AuditDetailsSanitizer.Sanitize(detailsJson) : null;

        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAt = occurredAt,
            ActorUserId = actorUserId,
            ActorType = actorType,
            DeploymentId = deploymentId,
            TenantId = tenantId,
            Action = action,
            TargetResource = targetResource,
            TargetId = targetId,
            Outcome = outcome,
            CorrelationId = correlationId,
            ClientIp = clientIp,
            DetailsJson = sanitizedDetails
        };
    }
}
