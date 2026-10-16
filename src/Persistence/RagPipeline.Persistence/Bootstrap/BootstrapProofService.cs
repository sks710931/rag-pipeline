namespace RagPipeline.Persistence.Bootstrap;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RagPipeline.BuildingBlocks.Audit;
using RagPipeline.BuildingBlocks.Bootstrap;
using RagPipeline.BuildingBlocks.Persistence;
using RagPipeline.Persistence.Entities;
using AuditEvent = RagPipeline.BuildingBlocks.Audit.AuditEvent;

public sealed class BootstrapProofService : IBootstrapProofService
{
    private readonly RagPipelineDbContext _dbContext;
    private readonly IBootstrapStateReader _stateReader;
    private readonly IBootstrapProofVerifier _verifier;
    private readonly IAuditEventStore? _auditEventStore;
    private readonly IRandomNumberProvider _randomProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BootstrapProofService> _logger;

    public BootstrapProofService(
        RagPipelineDbContext dbContext,
        IBootstrapStateReader stateReader,
        IBootstrapProofVerifier? verifier = null,
        IAuditEventStore? auditEventStore = null,
        IRandomNumberProvider? randomProvider = null,
        TimeProvider? timeProvider = null,
        ILogger<BootstrapProofService>? logger = null)
    {
        _dbContext = dbContext;
        _stateReader = stateReader;
        _verifier = verifier ?? new BootstrapProofVerifier();
        _auditEventStore = auditEventStore ?? new RagPipeline.Persistence.Audit.EfAuditEventStore(
            dbContext,
            new RagPipeline.Persistence.Tenancy.EfTenantTransactionCoordinator(dbContext));
        _randomProvider = randomProvider ?? new DefaultRandomNumberProvider();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<BootstrapProofService>.Instance;
    }

    public async Task<BootstrapProofGenerationResult> GenerateProofAsync(TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var rawState = await _stateReader.GetStateAsync(cancellationToken);
        if (rawState.IsSealed)
        {
            throw new InvalidOperationException("Cannot generate bootstrap proof: deployment is already SEALED.");
        }

        var token = BootstrapProofToken.Generate(_randomProvider);
        var duration = expiry ?? TimeSpan.FromMinutes(15);
        var expiresAt = _timeProvider.GetUtcNow().Add(duration);

        var rowsAffected = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE deployment_bootstrap_state
               SET bootstrap_proof_hash = {token.ProofHash},
                   proof_expires_at = {expiresAt},
                   proof_consumed_at = NULL
               WHERE id = 1 AND status <> 'SEALED';",
            cancellationToken);

        if (rowsAffected == 0)
        {
            var checkState = await _stateReader.GetStateAsync(cancellationToken);
            if (checkState.IsSealed)
            {
                throw new InvalidOperationException("Cannot generate bootstrap proof: deployment is already SEALED.");
            }
            throw new InvalidOperationException("Failed to store bootstrap proof: deployment state row not found or unavailable.");
        }

        if (_auditEventStore != null)
        {
            try
            {
                var audit = AuditEvent.Create(
                    actorType: "Operator",
                    action: "BootstrapProof.Generate",
                    outcome: "Success",
                    correlationId: Guid.NewGuid().ToString("N"),
                    occurredAt: _timeProvider.GetUtcNow(),
                    detailsJson: $"{{\"expiresAt\":\"{expiresAt:O}\",\"entropyBits\":{BootstrapProofToken.EntropyBits}}}");
                await _auditEventStore.AppendAsync(audit, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to append audit event for bootstrap proof generation.");
            }
        }

        return new BootstrapProofGenerationResult(token.PlaintextProof, token.ProofHash, expiresAt);
    }

    public async Task<ProofVerificationResult> VerifyAndConsumeProofAsync(string candidateProof, string? sourceIp = null, CancellationToken cancellationToken = default)
    {
        var state = await _dbContext.DeploymentBootstrapStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == DeploymentBootstrapState.SingletonId, cancellationToken);

        if (state == null)
        {
            return ProofVerificationResult.Absent("Bootstrap state row does not exist.");
        }

        var isSealed = string.Equals(state.Status, "SEALED", StringComparison.OrdinalIgnoreCase);

        var result = _verifier.VerifyProof(
            candidateProof: candidateProof,
            storedHash: state.BootstrapProofHash,
            expiresAt: state.ProofExpiresAt,
            consumedAt: state.ProofConsumedAt,
            isSealed: isSealed,
            sourceKey: sourceIp);

        if (!result.IsSuccess)
        {
            if (_auditEventStore != null)
            {
                try
                {
                    var audit = AuditEvent.Create(
                        actorType: "Anonymous",
                        action: "BootstrapProof.Verify",
                        outcome: "Failure",
                        correlationId: Guid.NewGuid().ToString("N"),
                        occurredAt: _timeProvider.GetUtcNow(),
                        detailsJson: $"{{\"reason\":\"{result.Status}\"}}");
                    await _auditEventStore.AppendAsync(audit, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to append audit event for bootstrap proof verification failure.");
                }
            }

            return result;
        }

        var candidateHash = BootstrapProofToken.ComputeHash(candidateProof);
        var rowsAffected = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE deployment_bootstrap_state
               SET proof_consumed_at = clock_timestamp()
               WHERE id = 1 AND status <> 'SEALED' AND proof_consumed_at IS NULL AND bootstrap_proof_hash = {candidateHash};",
            cancellationToken);

        if (rowsAffected != 1)
        {
            return ProofVerificationResult.AlreadyConsumed("Bootstrap proof has already been consumed concurrently.");
        }

        if (_auditEventStore != null)
        {
            try
            {
                var audit = AuditEvent.Create(
                    actorType: "Anonymous",
                    action: "BootstrapProof.Consume",
                    outcome: "Success",
                    correlationId: Guid.NewGuid().ToString("N"),
                    occurredAt: _timeProvider.GetUtcNow());
                await _auditEventStore.AppendAsync(audit, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to append audit event for bootstrap proof consumption.");
            }
        }

        return ProofVerificationResult.Success();
    }

    public async Task<bool> HasActiveProofAsync(CancellationToken cancellationToken = default)
    {
        var state = await _dbContext.DeploymentBootstrapStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == DeploymentBootstrapState.SingletonId, cancellationToken);

        if (state == null || string.IsNullOrWhiteSpace(state.BootstrapProofHash) || state.ProofConsumedAt.HasValue)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        return state.ProofExpiresAt.HasValue && now < state.ProofExpiresAt.Value;
    }
}
