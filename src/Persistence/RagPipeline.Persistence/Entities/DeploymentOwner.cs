namespace RagPipeline.Persistence.Entities;

public class DeploymentOwner
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Role { get; set; } = "DeploymentSuperadmin";
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? AssignedBy { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
