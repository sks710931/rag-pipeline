namespace RagPipeline.BuildingBlocks.Bootstrap;

public enum ProofVerificationStatus
{
    Success,
    Absent,
    Wrong,
    Expired,
    AlreadyConsumed,
    Sealed,
    LockedOut
}

public sealed record ProofVerificationResult(ProofVerificationStatus Status, string? Message = null)
{
    public bool IsSuccess => Status == ProofVerificationStatus.Success;

    public static ProofVerificationResult Success() => new(ProofVerificationStatus.Success);
    public static ProofVerificationResult Absent(string? message = null) => new(ProofVerificationStatus.Absent, message ?? "Proof is missing or not configured.");
    public static ProofVerificationResult Wrong(string? message = null) => new(ProofVerificationStatus.Wrong, message ?? "Invalid bootstrap proof.");
    public static ProofVerificationResult Expired(string? message = null) => new(ProofVerificationStatus.Expired, message ?? "Bootstrap proof has expired.");
    public static ProofVerificationResult AlreadyConsumed(string? message = null) => new(ProofVerificationStatus.AlreadyConsumed, message ?? "Bootstrap proof has already been consumed.");
    public static ProofVerificationResult Sealed(string? message = null) => new(ProofVerificationStatus.Sealed, message ?? "Deployment is already sealed.");
    public static ProofVerificationResult LockedOut(string? message = null) => new(ProofVerificationStatus.LockedOut, message ?? "Too many failed attempts. Try again later.");
}
