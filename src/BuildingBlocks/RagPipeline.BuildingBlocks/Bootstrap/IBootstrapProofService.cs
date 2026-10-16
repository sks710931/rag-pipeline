namespace RagPipeline.BuildingBlocks.Bootstrap;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IBootstrapProofService
{
    Task<BootstrapProofGenerationResult> GenerateProofAsync(TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task<ProofVerificationResult> VerifyAndConsumeProofAsync(string candidateProof, string? sourceIp = null, CancellationToken cancellationToken = default);
    Task<bool> HasActiveProofAsync(CancellationToken cancellationToken = default);
}
