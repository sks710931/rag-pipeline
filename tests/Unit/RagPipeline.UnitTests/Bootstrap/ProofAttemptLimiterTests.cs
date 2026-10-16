namespace RagPipeline.UnitTests.Bootstrap;

using System;
using RagPipeline.BuildingBlocks.Bootstrap;
using Xunit;

public class ProofAttemptLimiterTests
{
    [Fact]
    public void ProofAttemptLimiter_BlocksAfterNFailures_PerSourceAndGlobal()
    {
        var fakeClock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new ProofAttemptLimiter(
            maxFailuresPerSource: 3,
            maxGlobalFailures: 5,
            window: TimeSpan.FromMinutes(15),
            timeProvider: fakeClock);

        const string sourceA = "192.168.1.10";
        const string sourceB = "192.168.1.20";

        // Record 3 failures for sourceA
        Assert.False(limiter.IsBlocked(sourceA));
        limiter.RecordFailure(sourceA);
        Assert.False(limiter.IsBlocked(sourceA));
        limiter.RecordFailure(sourceA);
        Assert.False(limiter.IsBlocked(sourceA));
        limiter.RecordFailure(sourceA);

        // sourceA is now blocked
        Assert.True(limiter.IsBlocked(sourceA));

        // sourceB is NOT blocked yet (has 0 failures)
        Assert.False(limiter.IsBlocked(sourceB));

        // Record 2 more failures on sourceB -> total global failures reaches 5
        limiter.RecordFailure(sourceB);
        Assert.False(limiter.IsBlocked(sourceB));
        limiter.RecordFailure(sourceB);

        // Global limit (5) reached: both sourceB and any new source are blocked!
        Assert.True(limiter.IsBlocked(sourceB));
        Assert.True(limiter.IsBlocked("10.0.0.1"));
    }

    [Fact]
    public void ProofAttemptLimiter_ResetsAfterWindow()
    {
        var fakeClock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new ProofAttemptLimiter(
            maxFailuresPerSource: 3,
            maxGlobalFailures: 5,
            window: TimeSpan.FromMinutes(15),
            timeProvider: fakeClock);

        const string source = "192.168.1.50";

        limiter.RecordFailure(source);
        limiter.RecordFailure(source);
        limiter.RecordFailure(source);
        Assert.True(limiter.IsBlocked(source));

        // Advance clock past the 15-minute window
        fakeClock.Advance(TimeSpan.FromMinutes(16));

        // Lockout expired: source is unblocked
        Assert.False(limiter.IsBlocked(source));
    }

    [Fact]
    public void ProofAttemptLimiter_SuccessDoesNotBypass()
    {
        var fakeClock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new ProofAttemptLimiter(
            maxFailuresPerSource: 3,
            maxGlobalFailures: 5,
            window: TimeSpan.FromMinutes(15),
            timeProvider: fakeClock);

        const string sourceBad = "192.168.1.100";
        const string sourceGood = "192.168.1.200";

        limiter.RecordFailure(sourceBad);
        limiter.RecordFailure(sourceBad);
        limiter.RecordFailure(sourceBad);
        Assert.True(limiter.IsBlocked(sourceBad));

        // A success on sourceGood does not unblock sourceBad
        limiter.RecordSuccess(sourceGood);
        Assert.True(limiter.IsBlocked(sourceBad));
    }

    [Fact]
    public void ProofAttemptLimiter_LockoutDoesNotRevealWhetherProofWasCorrect()
    {
        var fakeClock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new ProofAttemptLimiter(
            maxFailuresPerSource: 2,
            window: TimeSpan.FromMinutes(15),
            timeProvider: fakeClock);

        const string source = "10.0.0.99";
        limiter.RecordFailure(source);
        limiter.RecordFailure(source);
        Assert.True(limiter.IsBlocked(source));

        var verifier = new BootstrapProofVerifier(timeProvider: fakeClock, limiter: limiter);
        var token = BootstrapProofToken.Generate();
        var expiresAt = fakeClock.GetUtcNow().AddMinutes(15);

        // Verification with correct proof returns LockedOut
        var resCorrect = verifier.VerifyProof(
            candidateProof: token.PlaintextProof,
            storedHash: token.ProofHash,
            expiresAt: expiresAt,
            consumedAt: null,
            isSealed: false,
            sourceKey: source);

        // Verification with completely wrong proof returns the identical LockedOut
        var resWrong = verifier.VerifyProof(
            candidateProof: "completely-wrong-proof",
            storedHash: token.ProofHash,
            expiresAt: expiresAt,
            consumedAt: null,
            isSealed: false,
            sourceKey: source);

        Assert.Equal(ProofVerificationStatus.LockedOut, resCorrect.Status);
        Assert.Equal(ProofVerificationStatus.LockedOut, resWrong.Status);
        Assert.Equal(resCorrect.Message, resWrong.Message);
    }
}
