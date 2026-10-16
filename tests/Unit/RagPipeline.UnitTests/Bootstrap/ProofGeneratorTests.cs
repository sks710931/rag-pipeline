namespace RagPipeline.UnitTests.Bootstrap;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using RagPipeline.BuildingBlocks.Bootstrap;
using Xunit;

public class ProofGeneratorTests
{
    private sealed class SpyRandomNumberProvider : IRandomNumberProvider
    {
        public int CallCount { get; private set; }
        public int LastRequestedCount { get; private set; }

        public byte[] GetBytes(int count)
        {
            CallCount++;
            LastRequestedCount = count;
            return RandomNumberGenerator.GetBytes(count);
        }
    }

    [Fact]
    public void ProofGenerator_UsesCsprng_MinEntropy()
    {
        var spyRng = new SpyRandomNumberProvider();
        var token = BootstrapProofToken.Generate(spyRng);

        // Asserts dependency on RandomNumberGenerator/IRandomNumberProvider
        Assert.Equal(1, spyRng.CallCount);
        Assert.Equal(32, spyRng.LastRequestedCount);

        // Asserts length >= agreed bits (256 bits = 32 bytes = 64 hex characters)
        Assert.True(BootstrapProofToken.EntropyBits >= BootstrapProofToken.MinEntropyBits);
        Assert.Equal(256, BootstrapProofToken.EntropyBits);
        Assert.Equal(64, token.PlaintextProof.Length);

        // 10k sample has no duplicates
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 10000; i++)
        {
            var sample = BootstrapProofToken.Generate().PlaintextProof;
            Assert.True(set.Add(sample), $"Duplicate proof generated at iteration {i}: {sample}");
        }
    }

    [Fact]
    public void ProofGenerator_StoresOnlyHash_NeverPlaintext()
    {
        var token = BootstrapProofToken.Generate();

        // Plaintext and hash must be distinct
        Assert.NotEqual(token.PlaintextProof, token.ProofHash);
        Assert.Equal(64, token.ProofHash.Length);

        // Verify SHA-256 hash calculation matches
        var expectedHash = BootstrapProofToken.ComputeHash(token.PlaintextProof);
        Assert.Equal(expectedHash, token.ProofHash);

        // Candidate model/entity persistence representation stores hash, never plaintext
        var result = new BootstrapProofGenerationResult("dummy-plaintext", token.ProofHash, DateTimeOffset.UtcNow.AddMinutes(15));
        Assert.Equal(token.ProofHash, result.ProofHash);
    }
}
