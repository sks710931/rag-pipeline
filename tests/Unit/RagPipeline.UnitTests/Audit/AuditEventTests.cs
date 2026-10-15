namespace RagPipeline.UnitTests.Audit;

using System;
using RagPipeline.BuildingBlocks.Audit;
using Xunit;

public class AuditEventTests
{
    [Theory]
    [InlineData("", "Tenant.Created", "Success", "corr-1")]
    [InlineData("   ", "Tenant.Created", "Success", "corr-1")]
    [InlineData(null, "Tenant.Created", "Success", "corr-1")]
    [InlineData("User", "", "Success", "corr-1")]
    [InlineData("User", "   ", "Success", "corr-1")]
    [InlineData("User", null, "Success", "corr-1")]
    [InlineData("User", "Tenant.Created", "", "corr-1")]
    [InlineData("User", "Tenant.Created", "   ", "corr-1")]
    [InlineData("User", "Tenant.Created", null, "corr-1")]
    [InlineData("User", "Tenant.Created", "Success", "")]
    [InlineData("User", "Tenant.Created", "Success", "   ")]
    [InlineData("User", "Tenant.Created", "Success", null)]
    public void AuditEvent_Create_RequiresActorActionOutcomeCorrelationAndTime(
        string? actorType,
        string? action,
        string? outcome,
        string? correlationId)
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() =>
            AuditEvent.Create(
                actorType: actorType!,
                action: action!,
                outcome: outcome!,
                correlationId: correlationId!,
                occurredAt: now));
    }

    [Fact]
    public void AuditEvent_Create_WithDefaultTimestamp_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            AuditEvent.Create(
                actorType: "User",
                action: "Tenant.Created",
                outcome: "Success",
                correlationId: "corr-1",
                occurredAt: default));
    }

    [Fact]
    public void AuditEvent_TenantScope_OptionalButExplicit()
    {
        var now = DateTimeOffset.UtcNow;
        var tenantId = Guid.NewGuid();

        // 1. Tenant-scoped event
        var tenantEvent = AuditEvent.Create(
            actorType: "User",
            action: "Tenant.Settings.Updated",
            outcome: "Success",
            correlationId: "corr-tenant",
            occurredAt: now,
            tenantId: tenantId);

        Assert.True(tenantEvent.IsTenantScoped);
        Assert.False(tenantEvent.IsDeploymentScoped);
        Assert.Equal(tenantId, tenantEvent.TenantId);

        // 2. Deployment-scoped event (no tenant ID)
        var deploymentEvent = AuditEvent.Create(
            actorType: "System",
            action: "Deployment.Bootstrap.Sealed",
            outcome: "Success",
            correlationId: "corr-deploy",
            occurredAt: now,
            tenantId: null);

        Assert.False(deploymentEvent.IsTenantScoped);
        Assert.True(deploymentEvent.IsDeploymentScoped);
        Assert.Null(deploymentEvent.TenantId);
    }
}
