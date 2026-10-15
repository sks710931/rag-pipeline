namespace RagPipeline.BuildingBlocks.Tenancy;

public interface ITenantTransactionCoordinator
{
    Task<TResult> ExecuteInTenantTransactionAsync<TResult>(
        Guid tenantId,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    Task ExecuteInTenantTransactionAsync(
        Guid tenantId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
