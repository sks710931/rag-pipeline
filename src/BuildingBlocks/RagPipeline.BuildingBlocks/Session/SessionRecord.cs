namespace RagPipeline.BuildingBlocks.Session;

using System;

public sealed record SessionRecord(
    Guid Id,
    Guid UserId,
    Guid RefreshTokenFamilyId,
    string RefreshTokenHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    bool IsRevoked,
    DateTimeOffset? RevokedAt,
    string? RevocationReason)
{
    public SessionRecord Revoke(string reason, DateTimeOffset? revokedAt = null)
    {
        if (IsRevoked)
        {
            throw new InvalidOperationException("Session is already revoked and cannot be revoked again.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Revocation reason cannot be null or empty.", nameof(reason));
        }

        return this with
        {
            IsRevoked = true,
            RevokedAt = revokedAt ?? DateTimeOffset.UtcNow,
            RevocationReason = reason
        };
    }
}

public interface ISessionRecordStore
{
    Task<SessionRecord> CreateSessionAsync(
        Guid userId,
        Guid refreshTokenFamilyId,
        string refreshTokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task<SessionRecord?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(
        Guid sessionId,
        string reason,
        DateTimeOffset? revokedAt = null,
        CancellationToken cancellationToken = default);
}
