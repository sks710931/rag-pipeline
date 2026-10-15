namespace RagPipeline.Persistence.Tenancy;

using System;
using System.Threading;
using System.Threading.Tasks;
using RagPipeline.BuildingBlocks.Tenancy;

public sealed class EfTenantTransactionCoordinator : ITenantTransactionCoordinator
{
    private readonly RagPipelineDbContext _dbContext;

    public EfTenantTransactionCoordinator(RagPipelineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<TResult> ExecuteInTenantTransactionAsync<TResult>(
        Guid tenantId,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var tx = await _dbContext.BeginTenantTransactionAsync(tenantId, cancellationToken);
        var result = await operation(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    public async Task ExecuteInTenantTransactionAsync(
        Guid tenantId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var tx = await _dbContext.BeginTenantTransactionAsync(tenantId, cancellationToken);
        await operation(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }
}
