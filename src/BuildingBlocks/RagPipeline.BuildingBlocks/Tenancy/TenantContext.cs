namespace RagPipeline.BuildingBlocks.Tenancy;

public sealed record TenantContext(Guid? TenantId, string? TenantRole) : ITenantContext
{
    public static readonly TenantContext None = new(null, null);

    public bool HasResolvedTenant => TenantId.HasValue;

    public static TenantContext Create(Guid tenantId, string tenantRole)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantRole);
        return new TenantContext(tenantId, tenantRole);
    }
}
