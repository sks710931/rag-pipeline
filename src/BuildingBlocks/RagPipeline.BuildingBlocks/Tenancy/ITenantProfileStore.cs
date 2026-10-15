namespace RagPipeline.BuildingBlocks.Tenancy;

public interface ITenantProfileStore
{
    Task<TenantProfileDto?> GetProfileAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TenantProfileDto> UpdateProfileAsync(Guid tenantId, string displayName, string settingsJson, CancellationToken cancellationToken = default);
}

public sealed record TenantProfileDto(Guid TenantId, string DisplayName, string SettingsJson, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
