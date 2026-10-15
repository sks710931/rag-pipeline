namespace RagPipeline.IntegrationTests.Isolation;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RagPipeline.IntegrationTests.Support;
using RagPipeline.Persistence.Entities;
using Xunit;

public class IsolationFixture : PostgresFixture
{
    public Guid TenantAId { get; } = Guid.NewGuid();
    public Guid TenantBId { get; } = Guid.NewGuid();

    public Guid UserAId { get; } = Guid.NewGuid();
    public Guid UserBId { get; } = Guid.NewGuid();
    public Guid UserBothId { get; } = Guid.NewGuid();

    public Guid AuditEventAId { get; } = Guid.NewGuid();
    public Guid AuditEventBId { get; } = Guid.NewGuid();

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!DockerAvailable)
        {
            return;
        }

        // 1. Loudly fail if the runtime role could bypass RLS or has superuser
        await using (var adminConn = new NpgsqlConnection(SuperuserConnectionString))
        {
            await adminConn.OpenAsync();

            await using var cmd = adminConn.CreateCommand();
            cmd.CommandText = @"
                SELECT rolname, rolsuper, rolbypassrls 
                FROM pg_roles 
                WHERE rolname IN ('rag_app', 'migrator');
            ";
            await using var reader = await cmd.ExecuteReaderAsync();
            var checkedRoles = 0;
            while (await reader.ReadAsync())
            {
                var roleName = reader.GetString(0);
                var isSuper = reader.GetBoolean(1);
                var bypassRls = reader.GetBoolean(2);

                if (isSuper || bypassRls)
                {
                    throw new InvalidOperationException(
                        $"Role '{roleName}' must not be superuser or bypass RLS (rolsuper={isSuper}, rolbypassrls={bypassRls}). Fixture aborted.");
                }

                checkedRoles++;
            }

            if (checkedRoles < 2)
            {
                throw new InvalidOperationException("Expected roles 'rag_app' and 'migrator' were not found in pg_roles.");
            }
        }

        // 2. Seed Users, Tenants, Memberships, Profiles, and AuditEvents
        await using var superContext = CreateSuperuserDbContext();

        var userA = new ApplicationUser
        {
            Id = UserAId,
            UserName = $"iso-user-a-{UserAId:N}",
            NormalizedUserName = $"ISO-USER-A-{UserAId:N}",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        var userB = new ApplicationUser
        {
            Id = UserBId,
            UserName = $"iso-user-b-{UserBId:N}",
            NormalizedUserName = $"ISO-USER-B-{UserBId:N}",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        var userBoth = new ApplicationUser
        {
            Id = UserBothId,
            UserName = $"iso-user-both-{UserBothId:N}",
            NormalizedUserName = $"ISO-USER-BOTH-{UserBothId:N}",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        superContext.Users.AddRange(userA, userB, userBoth);

        var tenantA = new Tenant
        {
            Id = TenantAId,
            Name = "Isolation Certification Tenant A",
            Slug = $"iso-a-{TenantAId:N}",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var tenantB = new Tenant
        {
            Id = TenantBId,
            Name = "Isolation Certification Tenant B",
            Slug = $"iso-b-{TenantBId:N}",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow
        };

        superContext.Tenants.AddRange(tenantA, tenantB);

        var membershipA = new TenantMembership
        {
            TenantId = TenantAId,
            UserId = UserAId,
            Role = "TenantOwner",
            IsFirstOwner = true,
            JoinedAt = DateTimeOffset.UtcNow
        };

        var membershipB = new TenantMembership
        {
            TenantId = TenantBId,
            UserId = UserBId,
            Role = "TenantOwner",
            IsFirstOwner = true,
            JoinedAt = DateTimeOffset.UtcNow
        };

        var membershipBothA = new TenantMembership
        {
            TenantId = TenantAId,
            UserId = UserBothId,
            Role = "TenantMember",
            IsFirstOwner = false,
            JoinedAt = DateTimeOffset.UtcNow
        };

        var membershipBothB = new TenantMembership
        {
            TenantId = TenantBId,
            UserId = UserBothId,
            Role = "TenantMember",
            IsFirstOwner = false,
            JoinedAt = DateTimeOffset.UtcNow
        };

        superContext.TenantMemberships.AddRange(membershipA, membershipB, membershipBothA, membershipBothB);

        var profileA = new TenantProfile
        {
            TenantId = TenantAId,
            DisplayName = "Tenant A Certified Profile",
            SettingsJson = "{\"isolation\":\"TenantA\"}",
            ConcurrencyToken = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var profileB = new TenantProfile
        {
            TenantId = TenantBId,
            DisplayName = "Tenant B Certified Profile",
            SettingsJson = "{\"isolation\":\"TenantB\"}",
            ConcurrencyToken = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        superContext.TenantProfiles.AddRange(profileA, profileB);

        var auditA = new AuditEvent
        {
            Id = AuditEventAId,
            TenantId = TenantAId,
            ActorUserId = UserAId,
            ActorType = "User",
            Action = "Isolation.Certification.ActionA",
            TargetResource = "TenantProfile",
            TargetId = TenantAId.ToString(),
            Outcome = "Success",
            CorrelationId = $"corr-iso-a-{TenantAId:N}",
            OccurredAt = DateTimeOffset.UtcNow
        };

        var auditB = new AuditEvent
        {
            Id = AuditEventBId,
            TenantId = TenantBId,
            ActorUserId = UserBId,
            ActorType = "User",
            Action = "Isolation.Certification.ActionB",
            TargetResource = "TenantProfile",
            TargetId = TenantBId.ToString(),
            Outcome = "Success",
            CorrelationId = $"corr-iso-b-{TenantBId:N}",
            OccurredAt = DateTimeOffset.UtcNow
        };

        superContext.AuditEvents.AddRange(auditA, auditB);

        await superContext.SaveChangesAsync();
    }
}

[CollectionDefinition("IsolationCollection")]
public class IsolationCollection : ICollectionFixture<IsolationFixture>
{
}
