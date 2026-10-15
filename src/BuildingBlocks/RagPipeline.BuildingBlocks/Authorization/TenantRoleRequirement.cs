namespace RagPipeline.BuildingBlocks.Authorization;

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using RagPipeline.BuildingBlocks.Tenancy;

public sealed class TenantRoleRequirement : IAuthorizationRequirement
{
    public string RequiredRole { get; }

    public TenantRoleRequirement(string requiredRole)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredRole);
        RequiredRole = requiredRole;
    }
}

public sealed class TenantRoleHandler : AuthorizationHandler<TenantRoleRequirement>
{
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public TenantRoleHandler(ITenantContextAccessor tenantContextAccessor)
    {
        _tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var tenantContext = _tenantContextAccessor.TenantContext;
        if (tenantContext == null)
        {
            return Task.CompletedTask;
        }

        if (string.Equals(tenantContext.TenantRole, requirement.RequiredRole, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
