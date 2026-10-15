namespace RagPipeline.Persistence.Audit;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RagPipeline.BuildingBlocks.Audit;
using RagPipeline.BuildingBlocks.Tenancy;
using EntityAuditEvent = RagPipeline.Persistence.Entities.AuditEvent;
using DomainAuditEvent = RagPipeline.BuildingBlocks.Audit.AuditEvent;

public class EfAuditEventStore : IAuditEventStore, IAuditRetentionService
{
    private readonly RagPipelineDbContext _dbContext;
    private readonly ITenantTransactionCoordinator _coordinator;

    public EfAuditEventStore(
        RagPipelineDbContext dbContext,
        ITenantTransactionCoordinator coordinator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public async Task AppendAsync(DomainAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (auditEvent == null)
        {
            throw new ArgumentNullException(nameof(auditEvent));
        }

        try
        {
            var entity = new EntityAuditEvent
            {
                Id = auditEvent.Id,
                OccurredAt = auditEvent.OccurredAt,
                ActorUserId = auditEvent.ActorUserId,
                ActorType = auditEvent.ActorType,
                DeploymentId = auditEvent.DeploymentId,
                TenantId = auditEvent.TenantId,
                Action = auditEvent.Action,
                TargetResource = auditEvent.TargetResource,
                TargetId = auditEvent.TargetId,
                Outcome = auditEvent.Outcome,
                CorrelationId = auditEvent.CorrelationId,
                ClientIp = auditEvent.ClientIp,
                DetailsJson = AuditDetailsSanitizer.Sanitize(auditEvent.DetailsJson)
            };

            if (auditEvent.IsTenantScoped && auditEvent.TenantId.HasValue)
            {
                var tenantId = auditEvent.TenantId.Value;
                if (_dbContext.ActiveTenantTransactionId == tenantId)
                {
                    _dbContext.AuditEvents.Add(entity);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    await _coordinator.ExecuteInTenantTransactionAsync(tenantId, async ct =>
                    {
                        _dbContext.AuditEvents.Add(entity);
                        await _dbContext.SaveChangesAsync(ct);
                    }, cancellationToken);
                }
            }
            else
            {
                _dbContext.AuditEvents.Add(entity);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            throw new AuditAppendException($"Failed to append audit event for action '{auditEvent.Action}'. Guarded operation must fail closed.", ex);
        }
    }

    public async Task<IReadOnlyList<DomainAuditEvent>> GetTenantEventsAsync(
        Guid tenantId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        return await _coordinator.ExecuteInTenantTransactionAsync(tenantId, async ct =>
        {
            var entities = await _dbContext.AuditEvents
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId)
                .OrderByDescending(e => e.OccurredAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);

            return (IReadOnlyList<DomainAuditEvent>)entities.Select(MapToDomain).ToList();
        }, cancellationToken);
    }

    public async Task<int> GetTenantEventsCountAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        return await _coordinator.ExecuteInTenantTransactionAsync(tenantId, async ct =>
        {
            return await _dbContext.AuditEvents
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId)
                .CountAsync(ct);
        }, cancellationToken);
    }

    public async Task<int> PurgeExpiredEventsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
    {
        if (retentionPeriod <= TimeSpan.Zero)
        {
            throw new ArgumentException("Retention period must be greater than zero.", nameof(retentionPeriod));
        }

        var cutoff = DateTimeOffset.UtcNow.Subtract(retentionPeriod);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await _dbContext.Database.OpenConnectionAsync(cancellationToken);
            }

            int deletedCount;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
                cmd.CommandText = "SELECT purge_expired_audit_events(@cutoff);";
                var param = cmd.CreateParameter();
                param.ParameterName = "@cutoff";
                param.Value = cutoff;
                cmd.Parameters.Add(param);
                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                deletedCount = Convert.ToInt32(result);
            }

            // The purge itself is audited
            var purgeEvent = new EntityAuditEvent
            {
                Id = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.UtcNow,
                ActorType = "System",
                Action = "Audit.Retention.Purged",
                TargetResource = "AuditEvent",
                Outcome = "Success",
                CorrelationId = Guid.NewGuid().ToString("N"),
                DetailsJson = $"{{\"deletedCount\":{deletedCount},\"cutoff\":\"{cutoff:O}\"}}"
            };

            _dbContext.AuditEvents.Add(purgeEvent);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return deletedCount;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static DomainAuditEvent MapToDomain(EntityAuditEvent entity)
    {
        return new DomainAuditEvent
        {
            Id = entity.Id,
            OccurredAt = entity.OccurredAt,
            ActorUserId = entity.ActorUserId,
            ActorType = entity.ActorType,
            DeploymentId = entity.DeploymentId,
            TenantId = entity.TenantId,
            Action = entity.Action,
            TargetResource = entity.TargetResource,
            TargetId = entity.TargetId,
            Outcome = entity.Outcome,
            CorrelationId = entity.CorrelationId ?? string.Empty,
            ClientIp = entity.ClientIp,
            DetailsJson = entity.DetailsJson
        };
    }
}
