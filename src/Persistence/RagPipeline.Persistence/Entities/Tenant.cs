namespace RagPipeline.Persistence.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public TenantProfile? Profile { get; set; }
    public ICollection<TenantMembership> Memberships { get; set; } = new List<TenantMembership>();
}
