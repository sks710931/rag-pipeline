namespace RagPipeline.BuildingBlocks.Authorization;

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

public sealed class KeyRootAndBootstrapAdminRequirement : IAuthorizationRequirement
{
}

public sealed class KeyRootAndBootstrapAdminHandler : AuthorizationHandler<KeyRootAndBootstrapAdminRequirement>
{
    private readonly IDeploymentOwnerLookup _deploymentOwnerLookup;

    public KeyRootAndBootstrapAdminHandler(IDeploymentOwnerLookup deploymentOwnerLookup)
    {
        _deploymentOwnerLookup = deploymentOwnerLookup ?? throw new ArgumentNullException(nameof(deploymentOwnerLookup));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        KeyRootAndBootstrapAdminRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId) || userId == Guid.Empty)
        {
            return;
        }

        // Revalidate against DB / persistent lookup to ensure Tenant Owner cannot reach deployment-only admin
        var isSuperadmin = await _deploymentOwnerLookup.IsDeploymentSuperadminAsync(userId);
        if (isSuperadmin)
        {
            context.Succeed(requirement);
        }
    }
}
