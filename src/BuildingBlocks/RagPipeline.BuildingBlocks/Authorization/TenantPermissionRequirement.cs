namespace RagPipeline.BuildingBlocks.Authorization;

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using RagPipeline.BuildingBlocks.Tenancy;

public sealed class TenantPermissionRequirement : IAuthorizationRequirement
{
    public string RequiredPermission { get; }

    public TenantPermissionRequirement(string requiredPermission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredPermission);
        RequiredPermission = requiredPermission;
    }
}

public sealed class TenantPermissionHandler : AuthorizationHandler<TenantPermissionRequirement>
{
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public TenantPermissionHandler(ITenantContextAccessor tenantContextAccessor)
    {
        _tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantPermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        // AC1: Strictly NO IsSuperadmin => allow bypass!
        // Tenant policies require verified server-side membership in the route tenant.
        var tenantContext = _tenantContextAccessor.TenantContext;
        if (tenantContext == null)
        {
            return Task.CompletedTask;
        }

        var permissions = RolePermissions.GetPermissions(tenantContext.TenantRole);
        if (permissions.Contains(requirement.RequiredPermission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
