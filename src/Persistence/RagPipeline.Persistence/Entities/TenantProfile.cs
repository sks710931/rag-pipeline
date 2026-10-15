namespace RagPipeline.Persistence.Entities;

public class TenantProfile
{
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string SettingsJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public Tenant Tenant { get; set; } = null!;
}
