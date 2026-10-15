namespace RagPipeline.IntegrationTests.Persistence;

using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RagPipeline.Persistence.Entities;
using Xunit;

[Collection("PostgresDatabase")]
public class SchemaConstraintTests
{
    private readonly PostgresFixture _fixture;

    public SchemaConstraintTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Schema_BootstrapSingleton_RejectsSecondRow()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var context = _fixture.CreateSuperuserDbContext();

        // Ensure singleton row 1 exists or can be inserted
        var existing = await context.DeploymentBootstrapStates.FindAsync(DeploymentBootstrapState.SingletonId);
        if (existing == null)
        {
            context.DeploymentBootstrapStates.Add(new DeploymentBootstrapState
            {
                Id = DeploymentBootstrapState.SingletonId,
                Status = "UNINITIALIZED"
            });
            await context.SaveChangesAsync();
        }

        // Attempt inserting row with id = 2 (violates chk_bootstrap_singleton: id = 1)
        var invalidSecond = new DeploymentBootstrapState
        {
            Id = 2,
            Status = "UNINITIALIZED"
        };
        context.DeploymentBootstrapStates.Add(invalidSecond);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Schema_FirstOwner_RejectsSecondFirstOwnerForSameTenant()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var context = _fixture.CreateSuperuserDbContext();

        var tenant1 = new Tenant { Name = "Tenant One", Slug = $"slug-1-{Guid.NewGuid():N}" };
        var tenant2 = new Tenant { Name = "Tenant Two", Slug = $"slug-2-{Guid.NewGuid():N}" };
        var user1 = new ApplicationUser { UserName = $"user1-{Guid.NewGuid():N}", NormalizedUserName = $"USER1-{Guid.NewGuid():N}", PasswordHash = "hash" };
        var user2 = new ApplicationUser { UserName = $"user2-{Guid.NewGuid():N}", NormalizedUserName = $"USER2-{Guid.NewGuid():N}", PasswordHash = "hash" };
        var user3 = new ApplicationUser { UserName = $"user3-{Guid.NewGuid():N}", NormalizedUserName = $"USER3-{Guid.NewGuid():N}", PasswordHash = "hash" };

        context.Tenants.AddRange(tenant1, tenant2);
        context.Users.AddRange(user1, user2, user3);
        await context.SaveChangesAsync();

        // First owner for Tenant 1
        context.TenantMemberships.Add(new TenantMembership
        {
            TenantId = tenant1.Id,
            UserId = user1.Id,
            Role = "TenantOwner",
            IsFirstOwner = true
        });
        await context.SaveChangesAsync();

        // First owner for Tenant 2 (different tenant is allowed)
        context.TenantMemberships.Add(new TenantMembership
        {
            TenantId = tenant2.Id,
            UserId = user3.Id,
            Role = "TenantOwner",
            IsFirstOwner = true
        });
        await context.SaveChangesAsync();

        // Second first owner for Tenant 1 must fail partial unique index uq_first_owner
        context.TenantMemberships.Add(new TenantMembership
        {
            TenantId = tenant1.Id,
            UserId = user2.Id,
            Role = "TenantOwner",
            IsFirstOwner = true
        });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Schema_Membership_IsImmutable()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var context = _fixture.CreateSuperuserDbContext();

        var tenant = new Tenant { Name = "Tenant Immut", Slug = $"slug-immut-{Guid.NewGuid():N}" };
        var user = new ApplicationUser { UserName = $"u-immut-{Guid.NewGuid():N}", NormalizedUserName = $"U-IMMUT-{Guid.NewGuid():N}", PasswordHash = "hash" };
        context.Tenants.Add(tenant);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var membership = new TenantMembership
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = "TenantMember",
            IsFirstOwner = false
        };
        context.TenantMemberships.Add(membership);
        await context.SaveChangesAsync();

        // Attempting to mutate role on existing membership must fail trigger
        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tenant_memberships SET role = 'TenantAdmin' WHERE tenant_id = @t AND user_id = @u;";
        cmd.Parameters.AddWithValue("t", tenant.Id);
        cmd.Parameters.AddWithValue("u", user.Id);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains("tenant_memberships is immutable", ex.Message);
    }

    [Fact]
    public async Task Schema_Usernames_UniqueCaseInsensitive()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var context = _fixture.CreateSuperuserDbContext();

        var uniqueSuffix = Guid.NewGuid().ToString("N");
        var user1 = new ApplicationUser
        {
            UserName = $"Admin_{uniqueSuffix}",
            NormalizedUserName = $"ADMIN_{uniqueSuffix}".ToUpperInvariant(),
            PasswordHash = "hash"
        };
        context.Users.Add(user1);
        await context.SaveChangesAsync();

        var user2 = new ApplicationUser
        {
            UserName = $"admin_{uniqueSuffix}",
            NormalizedUserName = $"admin_{uniqueSuffix}".ToUpperInvariant(),
            PasswordHash = "hash"
        };
        context.Users.Add(user2);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Schema_ForeignKeys_RejectOrphans()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var context = _fixture.CreateSuperuserDbContext();

        // Orphan membership (non-existent tenant & user)
        var orphanMembership = new TenantMembership
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Role = "TenantMember"
        };
        context.TenantMemberships.Add(orphanMembership);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());

        // Orphan auth session (non-existent user)
        context.ChangeTracker.Clear();
        var orphanSession = new AuthSession
        {
            UserId = Guid.NewGuid(),
            RefreshTokenFamilyId = Guid.NewGuid(),
            RefreshTokenHash = "hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };
        context.AuthSessions.Add(orphanSession);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Schema_AuditEvents_RejectUpdateAndDelete()
    {
        if (!_fixture.DockerAvailable)
        {
            return;
        }

        await using var context = _fixture.CreateSuperuserDbContext();

        var auditEvent = new AuditEvent
        {
            ActorType = "System",
            Action = "TestAudit",
            TargetResource = "System",
            Outcome = "Success",
            ClientIp = IPAddress.Loopback
        };
        context.AuditEvents.Add(auditEvent);
        await context.SaveChangesAsync();

        // Direct SQL update attempt as superuser/migrator
        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();

        await using var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = "UPDATE audit_events SET action = 'Hacked' WHERE id = @id;";
        updateCmd.Parameters.AddWithValue("id", auditEvent.Id);
        var updateEx = await Assert.ThrowsAsync<PostgresException>(() => updateCmd.ExecuteNonQueryAsync());
        Assert.Contains("audit_events is append-only", updateEx.Message);

        // Direct SQL delete attempt
        await using var deleteCmd = conn.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM audit_events WHERE id = @id;";
        deleteCmd.Parameters.AddWithValue("id", auditEvent.Id);
        var deleteEx = await Assert.ThrowsAsync<PostgresException>(() => deleteCmd.ExecuteNonQueryAsync());
        Assert.Contains("audit_events is append-only", deleteEx.Message);
    }
}
