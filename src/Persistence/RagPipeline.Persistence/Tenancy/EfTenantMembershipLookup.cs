namespace RagPipeline.Persistence.Tenancy;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagPipeline.BuildingBlocks.Tenancy;

public sealed class EfTenantMembershipLookup : ITenantMembershipLookup
{
    private readonly RagPipelineDbContext _dbContext;

    public EfTenantMembershipLookup(RagPipelineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<TenantMembershipInfo?> FindMembershipAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return null;
        }

        await using var tx = await _dbContext.BeginTenantTransactionAsync(tenantId, cancellationToken);
        var membership = await _dbContext.TenantMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        if (membership == null)
        {
            return null;
        }

        return new TenantMembershipInfo(
            membership.TenantId,
            membership.UserId,
            membership.Role,
            membership.IsFirstOwner);
    }
}
