namespace RagPipeline.BuildingBlocks.Bootstrap;

public interface IProofAttemptLimiter
{
    bool IsBlocked(string? sourceKey);
    void RecordFailure(string? sourceKey);
    void RecordSuccess(string? sourceKey);
    void Reset();
}
