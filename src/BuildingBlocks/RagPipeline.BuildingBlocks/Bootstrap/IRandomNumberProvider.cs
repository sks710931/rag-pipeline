namespace RagPipeline.BuildingBlocks.Bootstrap;

using System.Security.Cryptography;

public interface IRandomNumberProvider
{
    byte[] GetBytes(int count);
}

public sealed class DefaultRandomNumberProvider : IRandomNumberProvider
{
    public byte[] GetBytes(int count)
    {
        return RandomNumberGenerator.GetBytes(count);
    }
}
