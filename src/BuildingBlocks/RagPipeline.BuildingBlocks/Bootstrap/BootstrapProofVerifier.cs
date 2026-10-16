namespace RagPipeline.BuildingBlocks.Bootstrap;

using System;
using System.Text;

public interface IBootstrapProofVerifier
{
    ProofVerificationResult VerifyProof(
        string? candidateProof,
        string? storedHash,
        DateTimeOffset? expiresAt,
        DateTimeOffset? consumedAt,
        bool isSealed,
        string? sourceKey = null);
}

public sealed class BootstrapProofVerifier : IBootstrapProofVerifier
{
    private readonly IConstantTimeComparator _comparator;
    private readonly TimeProvider _timeProvider;
    private readonly IProofAttemptLimiter _limiter;

    public BootstrapProofVerifier(
        IConstantTimeComparator? comparator = null,
        TimeProvider? timeProvider = null,
        IProofAttemptLimiter? limiter = null)
    {
        _comparator = comparator ?? new DefaultConstantTimeComparator();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _limiter = limiter ?? new ProofAttemptLimiter();
    }

    public ProofVerificationResult VerifyProof(
        string? candidateProof,
        string? storedHash,
        DateTimeOffset? expiresAt,
        DateTimeOffset? consumedAt,
        bool isSealed,
        string? sourceKey = null)
    {
        if (_limiter.IsBlocked(sourceKey))
        {
            return ProofVerificationResult.LockedOut();
        }

        if (isSealed)
        {
            _limiter.RecordFailure(sourceKey);
            return ProofVerificationResult.Sealed();
        }

        if (string.IsNullOrWhiteSpace(candidateProof) || string.IsNullOrWhiteSpace(storedHash))
        {
            _limiter.RecordFailure(sourceKey);
            return ProofVerificationResult.Absent();
        }

        if (consumedAt.HasValue)
        {
            _limiter.RecordFailure(sourceKey);
            return ProofVerificationResult.AlreadyConsumed();
        }

        var now = _timeProvider.GetUtcNow();
        if (expiresAt.HasValue && now >= expiresAt.Value)
        {
            _limiter.RecordFailure(sourceKey);
            return ProofVerificationResult.Expired();
        }

        var candidateHash = BootstrapProofToken.ComputeHash(candidateProof);
        var candidateBytes = Encoding.UTF8.GetBytes(candidateHash);
        var storedBytes = Encoding.UTF8.GetBytes(storedHash.Trim().ToLowerInvariant());

        if (!_comparator.FixedTimeEquals(candidateBytes, storedBytes))
        {
            _limiter.RecordFailure(sourceKey);
            return ProofVerificationResult.Wrong();
        }

        _limiter.RecordSuccess(sourceKey);
        return ProofVerificationResult.Success();
    }
}
