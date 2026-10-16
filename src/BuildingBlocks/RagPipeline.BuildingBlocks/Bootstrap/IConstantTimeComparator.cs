namespace RagPipeline.BuildingBlocks.Bootstrap;

using System;
using System.Security.Cryptography;

public interface IConstantTimeComparator
{
    bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right);
}

public sealed class DefaultConstantTimeComparator : IConstantTimeComparator
{
    public bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
