namespace RagPipeline.Persistence.Session;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagPipeline.BuildingBlocks.Session;
using RagPipeline.Persistence.Entities;

public class EfSessionRecordStore : ISessionRecordStore
{
    private readonly RagPipelineDbContext _dbContext;

    public EfSessionRecordStore(RagPipelineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<SessionRecord> CreateSessionAsync(
        Guid userId,
        Guid refreshTokenFamilyId,
        string refreshTokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(refreshTokenHash))
        {
            throw new ArgumentException("RefreshTokenHash cannot be empty.", nameof(refreshTokenHash));
        }

        var now = DateTimeOffset.UtcNow;
        var session = new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RefreshTokenFamilyId = refreshTokenFamilyId == Guid.Empty ? Guid.NewGuid() : refreshTokenFamilyId,
            RefreshTokenHash = refreshTokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            LastSeenAt = now
        };

        _dbContext.AuthSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SessionRecord(
            session.Id,
            session.UserId,
            session.RefreshTokenFamilyId,
            session.RefreshTokenHash,
            session.ExpiresAt,
            session.CreatedAt,
            session.LastSeenAt,
            IsRevoked: false,
            RevokedAt: null,
            RevocationReason: null);
    }

    public async Task<SessionRecord?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return null;
        }

        var session = await _dbContext.AuthSessions
            .AsNoTracking()
            .Include(s => s.Revocation)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            return null;
        }

        var isRevoked = session.Revocation != null;
        return new SessionRecord(
            session.Id,
            session.UserId,
            session.RefreshTokenFamilyId,
            session.RefreshTokenHash,
            session.ExpiresAt,
            session.CreatedAt,
            session.LastSeenAt,
            isRevoked,
            session.Revocation?.RevokedAt,
            session.Revocation?.Reason);
    }

    public async Task RevokeSessionAsync(
        Guid sessionId,
        string reason,
        DateTimeOffset? revokedAt = null,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Revocation reason cannot be empty.", nameof(reason));
        }

        var session = await _dbContext.AuthSessions
            .Include(s => s.Revocation)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            throw new InvalidOperationException($"Session with ID '{sessionId}' was not found.");
        }

        if (session.Revocation != null)
        {
            throw new InvalidOperationException($"Session with ID '{sessionId}' is already revoked and cannot be revoked again.");
        }

        var revocation = new AuthSessionRevocation
        {
            SessionId = sessionId,
            RevokedAt = revokedAt ?? DateTimeOffset.UtcNow,
            Reason = reason
        };

        _dbContext.AuthSessionRevocations.Add(revocation);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
