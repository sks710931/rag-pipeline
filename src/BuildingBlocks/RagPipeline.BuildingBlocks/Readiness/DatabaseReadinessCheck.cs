namespace RagPipeline.BuildingBlocks.Readiness;

using Microsoft.Extensions.DependencyInjection;
using RagPipeline.BuildingBlocks.Persistence;

public sealed class DatabaseReadinessCheck : IReadinessCheck
{
    public const string DefaultName = "database";
    private readonly IServiceProvider? _serviceProvider;

    public DatabaseReadinessCheck(IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
    }

    public string Name => DefaultName;

    public async Task<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_serviceProvider == null)
        {
            return ReadinessResult.Unchecked();
        }

        using var scope = _serviceProvider.CreateScope();
        var reader = scope.ServiceProvider.GetService<IBootstrapStateReader>();
        if (reader == null)
        {
            return ReadinessResult.Unchecked();
        }

        var result = await reader.GetStateAsync(cancellationToken);
        if (result.IsUnavailable)
        {
            return ReadinessResult.NotReady(result.ReasonCode ?? "DATABASE_UNAVAILABLE");
        }

        if (result.IsSealed)
        {
            return ReadinessResult.Ready();
        }

        return ReadinessResult.NotReady("UNINITIALIZED");
    }
}
