namespace RagPipeline.BuildingBlocks.Tenancy;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public sealed class TenantContextResolver : ITenantContextResolver
{
    private readonly ITenantMembershipLookup _membershipLookup;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public TenantContextResolver(
        ITenantMembershipLookup membershipLookup,
        ITenantContextAccessor tenantContextAccessor)
    {
        _membershipLookup = membershipLookup ?? throw new ArgumentNullException(nameof(membershipLookup));
        _tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
    }

    public async Task<TenantResolutionResult> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // 1. Route validation - extract tenantId strictly from route data
        var routeTenantId = ExtractRouteTenantId(httpContext);
        if (!routeTenantId.HasValue || routeTenantId.Value == Guid.Empty)
        {
            return TenantResolutionResult.Denied(StatusCodes.Status404NotFound, "Tenant route parameter is missing or invalid.");
        }

        if (_tenantContextAccessor.TenantContext != null &&
            _tenantContextAccessor.TenantContext.TenantId == routeTenantId.Value)
        {
            return TenantResolutionResult.Success(_tenantContextAccessor.TenantContext);
        }

        // 2. Caller authentication check
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return TenantResolutionResult.Denied(StatusCodes.Status401Unauthorized, "Authentication is required to access tenant resources.");
        }

        // 3. Caller user id resolution
        var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId) || userId == Guid.Empty)
        {
            return TenantResolutionResult.Denied(StatusCodes.Status401Unauthorized, "Caller identity could not be resolved.");
        }

        // 4. Verify membership in the route tenant
        var membership = await _membershipLookup.FindMembershipAsync(routeTenantId.Value, userId, cancellationToken);
        if (membership == null)
        {
            // Fail-closed with 404 to prevent tenant enumeration
            return TenantResolutionResult.Denied(StatusCodes.Status404NotFound, "Tenant not found or access denied.");
        }

        // 5. Successful resolution: initialize tenant context strictly from verified server-side membership
        var context = TenantContext.Create(membership.TenantId, membership.Role);
        _tenantContextAccessor.TenantContext = context;

        return TenantResolutionResult.Success(context);
    }

    private static Guid? ExtractRouteTenantId(HttpContext httpContext)
    {
        var routeValue = httpContext.GetRouteValue("tenantId");
        if (routeValue is Guid guidValue)
        {
            return guidValue;
        }

        if (routeValue is string stringValue && Guid.TryParse(stringValue, out var parsedGuid))
        {
            return parsedGuid;
        }

        return null;
    }
}
