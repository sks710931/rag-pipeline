namespace RagPipeline.UnitTests.Bootstrap;

using System;
using RagPipeline.BuildingBlocks.Bootstrap;
using Xunit;

public class ProofVerifierTests
{
    private sealed class SpyConstantTimeComparator : IConstantTimeComparator
    {
        public int CallCount { get; private set; }
        public bool LastResult { get; private set; }

        public bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        {
            CallCount++;
            LastResult = System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(left, right);
            return LastResult;
        }
    }

    [Fact]
    public void ProofVerifier_ConstantTimeComparison()
    {
        var spyComparator = new SpyConstantTimeComparator();
        var verifier = new BootstrapProofVerifier(comparator: spyComparator);

        var token = BootstrapProofToken.Generate();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        var result = verifier.VerifyProof(
            candidateProof: token.PlaintextProof,
            storedHash: token.ProofHash,
            expiresAt: expiresAt,
            consumedAt: null,
            isSealed: false);

        Assert.Equal(ProofVerificationStatus.Success, result.Status);
        Assert.Equal(1, spyComparator.CallCount);
        Assert.True(spyComparator.LastResult);
    }

    [Theory]
    [InlineData(null, "stored-hash", false, false, -60, ProofVerificationStatus.Absent)]
    [InlineData("", "stored-hash", false, false, -60, ProofVerificationStatus.Absent)]
    [InlineData("valid-candidate", null, false, false, -60, ProofVerificationStatus.Absent)]
    [InlineData("valid-candidate", "", false, false, -60, ProofVerificationStatus.Absent)]
    [InlineData("wrong-candidate", "stored-hash", false, false, -60, ProofVerificationStatus.Wrong)]
    [InlineData("valid-candidate", "stored-hash", true, false, -60, ProofVerificationStatus.AlreadyConsumed)]
    [InlineData("valid-candidate", "stored-hash", false, true, -60, ProofVerificationStatus.Sealed)]
    [InlineData("valid-candidate", "stored-hash", false, false, -1, ProofVerificationStatus.Success)] // T - 1 sec: valid
    [InlineData("valid-candidate", "stored-hash", false, false, 0, ProofVerificationStatus.Expired)]  // T: expired boundary
    [InlineData("valid-candidate", "stored-hash", false, false, 1, ProofVerificationStatus.Expired)]  // T + 1 sec: expired
    public void ProofVerifier_Absent_Wrong_Expired_Consumed_AllRejected(
        string? candidateOverride,
        string? hashOverride,
        bool isConsumed,
        bool isSealed,
        int clockOffsetSecondsFromExpiry,
        ProofVerificationStatus expectedStatus)
    {
        var baseTime = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        var expiryTime = baseTime.AddMinutes(15);
        var testCurrentTime = expiryTime.AddSeconds(clockOffsetSecondsFromExpiry);

        var fakeClock = new FakeTimeProvider(testCurrentTime);
        var verifier = new BootstrapProofVerifier(timeProvider: fakeClock);

        var realToken = BootstrapProofToken.Generate();
        var candidateProof = candidateOverride == "valid-candidate" ? realToken.PlaintextProof : candidateOverride;
        var storedHash = hashOverride == "stored-hash" ? realToken.ProofHash : hashOverride;
        var consumedAt = isConsumed ? baseTime.AddMinutes(5) : (DateTimeOffset?)null;

        var result = verifier.VerifyProof(
            candidateProof: candidateProof,
            storedHash: storedHash,
            expiresAt: expiryTime,
            consumedAt: consumedAt,
            isSealed: isSealed);

        Assert.Equal(expectedStatus, result.Status);
    }

    [Fact]
    public void ProofVerifier_Consume_IsOneUse()
    {
        var fakeClock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var verifier = new BootstrapProofVerifier(timeProvider: fakeClock);

        var token = BootstrapProofToken.Generate();
        var expiresAt = fakeClock.GetUtcNow().AddMinutes(15);

        // 1. First verification with consumedAt = null succeeds
        var firstResult = verifier.VerifyProof(
            candidateProof: token.PlaintextProof,
            storedHash: token.ProofHash,
            expiresAt: expiresAt,
            consumedAt: null,
            isSealed: false);

        Assert.Equal(ProofVerificationStatus.Success, firstResult.Status);

        // 2. Mark consumed
        var consumedAt = fakeClock.GetUtcNow();

        // 3. Second verification fails with AlreadyConsumed
        var secondResult = verifier.VerifyProof(
            candidateProof: token.PlaintextProof,
            storedHash: token.ProofHash,
            expiresAt: expiresAt,
            consumedAt: consumedAt,
            isSealed: false);

        Assert.Equal(ProofVerificationStatus.AlreadyConsumed, secondResult.Status);
    }
}
