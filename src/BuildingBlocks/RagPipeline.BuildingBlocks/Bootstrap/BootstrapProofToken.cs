namespace RagPipeline.BuildingBlocks.Bootstrap;

using System;
using System.Security.Cryptography;
using System.Text;

public sealed record BootstrapProofToken
{
    public const int EntropyBytes = 32;
    public const int MinEntropyBits = 128;
    public const int EntropyBits = EntropyBytes * 8; // 256 bits

    public string PlaintextProof { get; }
    public string ProofHash { get; }

    private BootstrapProofToken(string plaintextProof, string proofHash)
    {
        PlaintextProof = plaintextProof;
        ProofHash = proofHash;
    }

    public static BootstrapProofToken Generate(IRandomNumberProvider? rng = null)
    {
        var provider = rng ?? new DefaultRandomNumberProvider();
        var bytes = provider.GetBytes(EntropyBytes);
        var plaintext = Convert.ToHexString(bytes).ToLowerInvariant();
        var hash = ComputeHash(plaintext);
        return new BootstrapProofToken(plaintext, hash);
    }

    public static string ComputeHash(string plaintextProof)
    {
        if (string.IsNullOrWhiteSpace(plaintextProof))
        {
            throw new ArgumentException("Plaintext proof cannot be null or whitespace.", nameof(plaintextProof));
        }

        var bytes = Encoding.UTF8.GetBytes(plaintextProof.Trim());
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
