namespace RagPipeline.Persistence.Entities;

public class TenantMembership
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "TenantOwner";
    public bool IsFirstOwner { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
