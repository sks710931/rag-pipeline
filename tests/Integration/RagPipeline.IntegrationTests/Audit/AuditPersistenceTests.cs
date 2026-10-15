namespace RagPipeline.IntegrationTests.Audit;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RagPipeline.BuildingBlocks.Audit;
using RagPipeline.IntegrationTests.Persistence;
using RagPipeline.Persistence.Audit;
using RagPipeline.Persistence.Entities;
using RagPipeline.Persistence.Session;
using RagPipeline.Persistence.Tenancy;
using Xunit;
using DomainAuditEvent = RagPipeline.BuildingBlocks.Audit.AuditEvent;
using EntityAuditEvent = RagPipeline.Persistence.Entities.AuditEvent;

[Collection("PostgresDatabase")]
public class AuditPersistenceTests
{
    private readonly PostgresFixture _fixture;

    public AuditPersistenceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Audit_Append_Succeeds_UpdateAndDeleteFail()
    {
        if (!_fixture.DockerAvailable) return;

        var eventId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();

        // 1. Append succeeds as runtime role (rag_app)
        await using (var runtimeContext = _fixture.CreateRuntimeDbContext())
        {
            var auditEvent = new EntityAuditEvent
            {
                Id = eventId,
                OccurredAt = DateTimeOffset.UtcNow,
                ActorType = "System",
                DeploymentId = deploymentId,
                TenantId = null,
                Action = "Deployment.Bootstrap.Sealed",
                TargetResource = "Deployment",
                Outcome = "Success",
                CorrelationId = "corr-append-test",
                DetailsJson = "{\"status\":\"sealed\"}"
            };

            runtimeContext.AuditEvents.Add(auditEvent);
            await runtimeContext.SaveChangesAsync();
        }

        // 2. Direct UPDATE fails for runtime role (rag_app)
        await using (var runtimeContext = _fixture.CreateRuntimeDbContext())
        {
            await Assert.ThrowsAnyAsync<PostgresException>(async () =>
            {
                await runtimeContext.Database.ExecuteSqlRawAsync(
                    "UPDATE audit_events SET action = 'tampered' WHERE id = {0};", eventId);
            });
        }

        // 3. Direct DELETE fails for runtime role (rag_app)
        await using (var runtimeContext = _fixture.CreateRuntimeDbContext())
        {
            await Assert.ThrowsAnyAsync<PostgresException>(async () =>
            {
                await runtimeContext.Database.ExecuteSqlRawAsync(
                    "DELETE FROM audit_events WHERE id = {0};", eventId);
            });
        }

        // 4. Direct UPDATE, DELETE, and TRUNCATE fail for migrator role
        await using (var migratorContext = _fixture.CreateMigratorDbContext())
        {
            await Assert.ThrowsAnyAsync<PostgresException>(async () =>
            {
                await migratorContext.Database.ExecuteSqlRawAsync(
                    "UPDATE audit_events SET action = 'tampered-by-migrator' WHERE id = {0};", eventId);
            });

            await Assert.ThrowsAnyAsync<PostgresException>(async () =>
            {
                await migratorContext.Database.ExecuteSqlRawAsync(
                    "DELETE FROM audit_events WHERE id = {0};", eventId);
            });

            await Assert.ThrowsAnyAsync<PostgresException>(async () =>
            {
                await migratorContext.Database.ExecuteSqlRawAsync("TRUNCATE audit_events;");
            });
        }
    }

    [Fact]
    public async Task Audit_Readback_TenantA_DoesNotSeeTenantBOrDeploymentEvents()
    {
        if (!_fixture.DockerAvailable) return;

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var idDeploy = Guid.NewGuid();

        // Seed via superuser context
        await using (var superContext = _fixture.CreateSuperuserDbContext())
        {
            superContext.AuditEvents.Add(new EntityAuditEvent
            {
                Id = idA,
                TenantId = tenantA,
                ActorType = "User",
                Action = "Tenant.ActionA",
                TargetResource = "Tenant",
                Outcome = "Success",
                CorrelationId = "corr-a"
            });
            superContext.AuditEvents.Add(new EntityAuditEvent
            {
                Id = idB,
                TenantId = tenantB,
                ActorType = "User",
                Action = "Tenant.ActionB",
                TargetResource = "Tenant",
                Outcome = "Success",
                CorrelationId = "corr-b"
            });
            superContext.AuditEvents.Add(new EntityAuditEvent
            {
                Id = idDeploy,
                TenantId = null,
                ActorType = "System",
                Action = "Deployment.Action",
                TargetResource = "Deployment",
                Outcome = "Success",
                CorrelationId = "corr-deploy"
            });
            await superContext.SaveChangesAsync();
        }

        // Readback via runtime context in Tenant A's transaction
        await using (var runtimeContext = _fixture.CreateRuntimeDbContext())
        {
            await using var tx = await runtimeContext.BeginTenantTransactionAsync(tenantA);

            var events = await runtimeContext.AuditEvents.AsNoTracking().ToListAsync();

            Assert.Contains(events, e => e.Id == idA && e.TenantId == tenantA);
            Assert.DoesNotContain(events, e => e.Id == idB);
            Assert.DoesNotContain(events, e => e.Id == idDeploy);
            Assert.DoesNotContain(events, e => e.TenantId == tenantB);
            Assert.DoesNotContain(events, e => e.TenantId == null);
        }
    }

    [Fact]
    public async Task Audit_Readback_UnsetTenantContext_ReturnsNothing()
    {
        if (!_fixture.DockerAvailable) return;

        var tenantA = Guid.NewGuid();
        var idA = Guid.NewGuid();

        await using (var superContext = _fixture.CreateSuperuserDbContext())
        {
            superContext.AuditEvents.Add(new EntityAuditEvent
            {
                Id = idA,
                TenantId = tenantA,
                ActorType = "User",
                Action = "Tenant.ActionA",
                TargetResource = "Tenant",
                Outcome = "Success",
                CorrelationId = "corr-readback-unset"
            });
            await superContext.SaveChangesAsync();
        }

        // Without setting tenant context, SELECT returns 0 rows due to RLS
        await using (var runtimeContext = _fixture.CreateRuntimeDbContext())
        {
            var events = await runtimeContext.AuditEvents.AsNoTracking().ToListAsync();
            Assert.DoesNotContain(events, e => e.Id == idA);
        }
    }

    [Fact]
    public async Task Audit_Write_WithoutTenantContext_AllowedOnlyForDeploymentScope()
    {
        if (!_fixture.DockerAvailable) return;

        var deployEventId = Guid.NewGuid();
        var tenantEventId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // 1. Without tenant context, writing deployment event (tenant_id IS NULL) succeeds
        await using (var runtimeContext = _fixture.CreateRuntimeDbContext())
        {
            runtimeContext.AuditEvents.Add(new EntityAuditEvent
            {
                Id = deployEventId,
                TenantId = null,
                ActorType = "System",
                Action = "Deployment.Bootstrap.Init",
                TargetResource = "Deployment",
                Outcome = "Success",
                CorrelationId = "corr-deploy-write"
            });
            await runtimeContext.SaveChangesAsync();
        }

        // 2. Without tenant context, writing tenant-scoped event (tenant_id != NULL) fails RLS WITH CHECK
        await using (var runtimeContext = _fixture.CreateRuntimeDbContext())
        {
            runtimeContext.AuditEvents.Add(new EntityAuditEvent
            {
                Id = tenantEventId,
                TenantId = tenantId,
                ActorType = "User",
                Action = "Tenant.IllegalWrite",
                TargetResource = "Tenant",
                Outcome = "Success",
                CorrelationId = "corr-tenant-write"
            });

            await Assert.ThrowsAnyAsync<DbUpdateException>(async () =>
            {
                await runtimeContext.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task Audit_AppendFailure_FailsTheGuardedOperation()
    {
        if (!_fixture.DockerAvailable) return;

        await using var runtimeContext = _fixture.CreateRuntimeDbContext();
        var coordinator = new EfTenantTransactionCoordinator(runtimeContext);
        var store = new EfAuditEventStore(runtimeContext, coordinator);

        // Intentionally create an invalid audit event that violates DB constraint
        var invalidEvent = new DomainAuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            ActorType = new string('X', 50), // exceeds 32 char max length
            Action = "Test.Invalid",
            Outcome = "Failure",
            CorrelationId = "corr-fail"
        };

        // Assert that append throws AuditAppendException to fail closed
        await Assert.ThrowsAsync<AuditAppendException>(async () =>
        {
            await store.AppendAsync(invalidEvent);
        });
    }

    [Fact]
    public async Task Session_Revocation_PersistsAcrossContextInstances()
    {
        if (!_fixture.DockerAvailable) return;

        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        // 1. Seed user via superuser context
        await using (var superContext = _fixture.CreateSuperuserDbContext())
        {
            superContext.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = $"user-session-{userId:N}",
                NormalizedUserName = $"USER-SESSION-{userId:N}",
                PasswordHash = "hash",
                SecurityStamp = "stamp",
                ConcurrencyStamp = "cstamp"
            });
            await superContext.SaveChangesAsync();
        }

        // 2. Create session and revoke it in Scope 1
        await using (var context1 = _fixture.CreateRuntimeDbContext())
        {
            var sessionStore = new EfSessionRecordStore(context1);
            var created = await sessionStore.CreateSessionAsync(
                userId,
                familyId,
                "token-hash-abc",
                DateTimeOffset.UtcNow.AddDays(7));

            sessionId = created.Id;
            Assert.False(created.IsRevoked);

            await sessionStore.RevokeSessionAsync(sessionId, "User logout request");
        }

        // 3. Verify revocation persists durable in Scope 2 (brand new DbContext instance)
        await using (var context2 = _fixture.CreateRuntimeDbContext())
        {
            var sessionStore = new EfSessionRecordStore(context2);
            var retrieved = await sessionStore.GetSessionAsync(sessionId);

            Assert.NotNull(retrieved);
            Assert.True(retrieved.IsRevoked);
            Assert.Equal("User logout request", retrieved.RevocationReason);
            Assert.NotNull(retrieved.RevokedAt);

            // One-way revocation: second revocation attempt throws
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await sessionStore.RevokeSessionAsync(sessionId, "Attempt repeat revoke");
            });
        }
    }

    [Fact]
    public async Task Retention_PurgeJob_OnlyDeletesPastRetention_AndIsItselfAudited()
    {
        if (!_fixture.DockerAvailable) return;

        var oldEventId = Guid.NewGuid();
        var recentEventId = Guid.NewGuid();

        // Seed old event (> 30 days old) and recent event (< 30 days old) via superuser
        await using (var superContext = _fixture.CreateSuperuserDbContext())
        {
            superContext.AuditEvents.Add(new EntityAuditEvent
            {
                Id = oldEventId,
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-60),
                ActorType = "System",
                Action = "Old.Event",
                Outcome = "Success",
                CorrelationId = "corr-old"
            });
            superContext.AuditEvents.Add(new EntityAuditEvent
            {
                Id = recentEventId,
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-5),
                ActorType = "System",
                Action = "Recent.Event",
                Outcome = "Success",
                CorrelationId = "corr-recent"
            });
            await superContext.SaveChangesAsync();
        }

        // Run purge via EfAuditEventStore
        await using (var runtimeContext = _fixture.CreateRuntimeDbContext())
        {
            var coordinator = new EfTenantTransactionCoordinator(runtimeContext);
            var store = new EfAuditEventStore(runtimeContext, coordinator);

            var deleted = await store.PurgeExpiredEventsAsync(TimeSpan.FromDays(30));
            Assert.True(deleted >= 1);
        }

        // Verify old event was purged, recent event survived, and purge event was audited
        await using (var superContext = _fixture.CreateSuperuserDbContext())
        {
            var oldEvent = await superContext.AuditEvents.FirstOrDefaultAsync(e => e.Id == oldEventId);
            var recentEvent = await superContext.AuditEvents.FirstOrDefaultAsync(e => e.Id == recentEventId);
            var purgeAudit = await superContext.AuditEvents.FirstOrDefaultAsync(e => e.Action == "Audit.Retention.Purged");

            Assert.Null(oldEvent);
            Assert.NotNull(recentEvent);
            Assert.NotNull(purgeAudit);
            Assert.Contains("deletedCount", purgeAudit.DetailsJson);
        }
    }
}
