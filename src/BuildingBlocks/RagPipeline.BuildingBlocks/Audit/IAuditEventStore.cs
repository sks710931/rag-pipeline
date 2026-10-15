namespace RagPipeline.BuildingBlocks.Audit;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IAuditEventStore
{
    Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEvent>> GetTenantEventsAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken = default);
    Task<int> GetTenantEventsCountAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public interface IAuditRetentionService
{
    Task<int> PurgeExpiredEventsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
}

public sealed class AuditAppendException : Exception
{
    public AuditAppendException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
