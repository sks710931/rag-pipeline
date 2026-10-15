namespace RagPipeline.Persistence.Tenancy;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagPipeline.BuildingBlocks.Tenancy;

public sealed class EfTenantProfileStore : ITenantProfileStore
{
    private readonly RagPipelineDbContext _dbContext;

    public EfTenantProfileStore(RagPipelineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<TenantProfileDto?> GetProfileAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        await using var tx = await _dbContext.BeginTenantTransactionAsync(tenantId, cancellationToken);
        var profile = await _dbContext.TenantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        if (profile == null)
        {
            return null;
        }

        return new TenantProfileDto(
            profile.TenantId,
            profile.DisplayName,
            profile.SettingsJson,
            profile.CreatedAt,
            profile.UpdatedAt);
    }

    public async Task<TenantProfileDto> UpdateProfileAsync(
        Guid tenantId,
        string displayName,
        string settingsJson,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsJson);

        await using var tx = await _dbContext.BeginTenantTransactionAsync(tenantId, cancellationToken);
        var profile = await _dbContext.TenantProfiles
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);

        if (profile == null)
        {
            throw new KeyNotFoundException($"Tenant profile for tenant {tenantId} not found.");
        }

        profile.DisplayName = displayName;
        profile.SettingsJson = settingsJson;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new TenantProfileDto(
            profile.TenantId,
            profile.DisplayName,
            profile.SettingsJson,
            profile.CreatedAt,
            profile.UpdatedAt);
    }
}
