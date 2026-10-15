namespace RagPipeline.BuildingBlocks.Tenancy;

public interface ITenantMembershipLookup
{
    Task<TenantMembershipInfo?> FindMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed record TenantMembershipInfo(Guid TenantId, Guid UserId, string Role, bool IsFirstOwner);
