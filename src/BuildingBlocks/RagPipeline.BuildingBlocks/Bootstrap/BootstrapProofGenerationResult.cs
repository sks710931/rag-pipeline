namespace RagPipeline.BuildingBlocks.Bootstrap;

using System;

public sealed record BootstrapProofGenerationResult(
    string PlaintextProof,
    string ProofHash,
    DateTimeOffset ExpiresAt);
