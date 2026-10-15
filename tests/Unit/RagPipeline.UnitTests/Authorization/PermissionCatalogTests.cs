namespace RagPipeline.UnitTests.Authorization;

using System.Linq;
using RagPipeline.BuildingBlocks.Authorization;
using Xunit;

public class PermissionCatalogTests
{
    [Fact]
    public void PermissionCatalog_DeploymentAndTenantPermissions_AreDisjoint()
    {
        // AC2: Deployment permissions and Tenant permissions must never overlap
        var intersection = DeploymentPermissions.All.Intersect(TenantPermissions.All).ToList();

        Assert.Empty(intersection);
    }

    [Fact]
    public void TenantOwner_DoesNotImplyContentRead()
    {
        // Requirement 3: Tenant Owner has settings/membership permissions but not document/content read
        var ownerPermissions = RolePermissions.GetPermissions(Roles.TenantOwner);

        Assert.NotEmpty(ownerPermissions);
        Assert.Contains(TenantPermissions.SettingsRead, ownerPermissions);
        Assert.Contains(TenantPermissions.SettingsWrite, ownerPermissions);
        Assert.Contains(TenantPermissions.MembersManage, ownerPermissions);

        var contentOverlap = ownerPermissions.Intersect(DocumentPermissions.All).ToList();
        Assert.Empty(contentOverlap);

        Assert.DoesNotContain(DocumentPermissions.DocumentRead, ownerPermissions);
        Assert.DoesNotContain(DocumentPermissions.DocumentWrite, ownerPermissions);
        Assert.DoesNotContain(DocumentPermissions.ContentClassifiedRead, ownerPermissions);
    }
}
