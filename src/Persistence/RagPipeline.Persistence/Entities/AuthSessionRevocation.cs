namespace RagPipeline.Persistence.Entities;

public class AuthSessionRevocation
{
    public Guid SessionId { get; set; }
    public DateTimeOffset RevokedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Reason { get; set; } = string.Empty;

    public AuthSession Session { get; set; } = null!;
}
