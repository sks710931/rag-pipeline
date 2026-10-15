namespace RagPipeline.Persistence.Entities;

public class DeploymentBootstrapState
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string Status { get; set; } = "UNINITIALIZED";
    public string? BootstrapProofHash { get; set; }
    public DateTimeOffset? ProofExpiresAt { get; set; }
    public DateTimeOffset? ProofConsumedAt { get; set; }
    public DateTimeOffset? SealedAt { get; set; }
    public Guid? SealedByUserId { get; set; }

    public ApplicationUser? SealedByUser { get; set; }
}
