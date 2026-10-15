namespace RagPipeline.BuildingBlocks.Tenancy;

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<ITenantContext?> AsyncLocalFallback = new();
    private ITenantContext? _tenantContext;

    public ITenantContext TenantContext
    {
        get => _tenantContext ?? AsyncLocalFallback.Value ?? ITenantContext.None;
        set
        {
            _tenantContext = value ?? ITenantContext.None;
            AsyncLocalFallback.Value = value ?? ITenantContext.None;
        }
    }
}
