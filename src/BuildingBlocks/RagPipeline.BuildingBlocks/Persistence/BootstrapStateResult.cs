namespace RagPipeline.BuildingBlocks.Persistence;

public enum BootstrapStateKind
{
    Uninitialized,
    Sealed,
    Unavailable
}

public sealed record BootstrapStateResult
{
    public BootstrapStateKind Kind { get; }

    public string? ReasonCode { get; }

    private BootstrapStateResult(BootstrapStateKind kind, string? reasonCode)
    {
        Kind = kind;
        ReasonCode = reasonCode;
    }

    public bool IsUninitialized => Kind == BootstrapStateKind.Uninitialized;

    public bool IsSealed => Kind == BootstrapStateKind.Sealed;

    public bool IsUnavailable => Kind == BootstrapStateKind.Unavailable;

    public static BootstrapStateResult Uninitialized() => new(BootstrapStateKind.Uninitialized, null);

    public static BootstrapStateResult Sealed() => new(BootstrapStateKind.Sealed, null);

    public static BootstrapStateResult Unavailable(string reasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new(BootstrapStateKind.Unavailable, reasonCode);
    }
}
