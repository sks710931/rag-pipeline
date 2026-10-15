namespace RagPipeline.BuildingBlocks.Tenancy;

public interface ITenantContextAccessor
{
    ITenantContext TenantContext { get; set; }
}
