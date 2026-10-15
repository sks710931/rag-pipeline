namespace RagPipeline.BuildingBlocks.Tenancy;

public interface ITenantContext
{
    Guid? TenantId { get; }

    string? TenantRole { get; }

    bool HasResolvedTenant => TenantId.HasValue;

    public static ITenantContext None => RagPipeline.BuildingBlocks.Tenancy.TenantContext.None;
}
