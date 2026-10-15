namespace RagPipeline.IntegrationTests.Isolation;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

[Collection("IsolationCollection")]
[Trait("Category", "Isolation")]
public class IsolationFixtureTests
{
    private readonly IsolationFixture _fixture;

    public IsolationFixtureTests(IsolationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task IsolationFixture_SeedsTwoTenantsTwoMembershipsAndNonOwnerRuntimeRole()
    {
        Assert.True(_fixture.DockerAvailable, "Docker must be available for isolation certification suite (AC3 / E2E-07).");

        await using var superContext = _fixture.CreateSuperuserDbContext();

        // 1. Verify two tenants seeded
        var tenantA = await superContext.Tenants.FindAsync(_fixture.TenantAId);
        var tenantB = await superContext.Tenants.FindAsync(_fixture.TenantBId);
        Assert.NotNull(tenantA);
        Assert.NotNull(tenantB);

        // 2. Verify rows in every tenant table for Tenant A
        var profileA = await superContext.TenantProfiles.FindAsync(_fixture.TenantAId);
        var membershipsA = await superContext.TenantMemberships
            .Where(m => m.TenantId == _fixture.TenantAId)
            .ToListAsync();
        var auditA = await superContext.AuditEvents
            .Where(a => a.TenantId == _fixture.TenantAId)
            .ToListAsync();

        Assert.NotNull(profileA);
        Assert.True(membershipsA.Count >= 2, "Tenant A must have at least 2 memberships (UserA and UserBoth).");
        Assert.NotEmpty(auditA);

        // 3. Verify rows in every tenant table for Tenant B
        var profileB = await superContext.TenantProfiles.FindAsync(_fixture.TenantBId);
        var membershipsB = await superContext.TenantMemberships
            .Where(m => m.TenantId == _fixture.TenantBId)
            .ToListAsync();
        var auditB = await superContext.AuditEvents
            .Where(a => a.TenantId == _fixture.TenantBId)
            .ToListAsync();

        Assert.NotNull(profileB);
        Assert.True(membershipsB.Count >= 2, "Tenant B must have at least 2 memberships (UserB and UserBoth).");
        Assert.NotEmpty(auditB);

        // 4. Verify UserBoth is a member of BOTH tenants
        var userBothMemberships = await superContext.TenantMemberships
            .Where(m => m.UserId == _fixture.UserBothId)
            .ToListAsync();
        Assert.Equal(2, userBothMemberships.Count);
        Assert.Contains(userBothMemberships, m => m.TenantId == _fixture.TenantAId);
        Assert.Contains(userBothMemberships, m => m.TenantId == _fixture.TenantBId);

        // 5. Verify non-owner runtime role and non-superuser NOBYPASSRLS privileges in PostgreSQL
        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();

        await using (var roleCmd = conn.CreateCommand())
        {
            roleCmd.CommandText = @"
                SELECT rolname, rolsuper, rolbypassrls 
                FROM pg_roles 
                WHERE rolname IN ('rag_app', 'migrator');
            ";
            await using var reader = await roleCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var roleName = reader.GetString(0);
                var isSuper = reader.GetBoolean(1);
                var bypassRls = reader.GetBoolean(2);

                Assert.False(isSuper, $"Role '{roleName}' must not be superuser.");
                Assert.False(bypassRls, $"Role '{roleName}' must have NOBYPASSRLS.");
            }
        }

        // 6. Verify runtime role owns ZERO tables in public schema
        await using (var ownerCmd = conn.CreateCommand())
        {
            ownerCmd.CommandText = "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tableowner = 'rag_app';";
            var runtimeOwnedCount = Convert.ToInt32(await ownerCmd.ExecuteScalarAsync());
            Assert.Equal(0, runtimeOwnedCount);
        }

        // 7. Verify migrator owns the public tables
        await using (var migratorOwnerCmd = conn.CreateCommand())
        {
            migratorOwnerCmd.CommandText = "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tableowner = 'migrator';";
            var migratorOwnedCount = Convert.ToInt32(await migratorOwnerCmd.ExecuteScalarAsync());
            Assert.True(migratorOwnedCount > 0, "Migrator role must own public tables.");
        }
    }
}
