namespace RagPipeline.UnitTests.Authorization;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RagPipeline.BuildingBlocks.Authorization;
using RagPipeline.BuildingBlocks.Modules;
using RagPipeline.BuildingBlocks.Tenancy;
using RagPipeline.DeploymentCapabilities;
using RagPipeline.IdentityAccess;
using RagPipeline.TenantManagement;
using Xunit;

public class AuthorizationHandlerTests
{
    [Fact]
    public async Task SuperadminHandler_NoMembership_DoesNotSatisfyTenantPolicy()
    {
        // AC1: Platform Superadmin without explicit tenant membership cannot access tenant settings/resources.
        // There is strictly NO "IsSuperadmin => allow" path.
        var superadminUserId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, superadminUserId.ToString()),
            new Claim(ClaimTypes.Role, Roles.DeploymentSuperadmin)
        ], "TestAuth"));

        // TenantContext is None / null (no membership in route tenant)
        var emptyTenantAccessor = new FakeTenantContextAccessor(TenantContext.None);
        var handler = new TenantPermissionHandler(emptyTenantAccessor);

        var requirement = new TenantPermissionRequirement(TenantPermissions.SettingsRead);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded, "TenantPermissionHandler must fail when caller has no tenant membership, even if caller is DeploymentSuperadmin.");
    }

    [Fact]
    public async Task TenantOwnerHandler_NoDeploymentGrant_DoesNotSatisfyDeploymentPolicy()
    {
        // AC2: Tenant Owner cannot reach deployment-only admin without explicit deployment grant
        var ownerUserId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, ownerUserId.ToString()),
            new Claim(ClaimTypes.Role, Roles.TenantOwner)
        ], "TestAuth"));

        var fakeDeploymentLookup = new FakeDeploymentOwnerLookup(superadminUserIds: []);
        var adminHandler = new DeploymentAdminHandler(fakeDeploymentLookup);
        var keyRootHandler = new KeyRootAndBootstrapAdminHandler(fakeDeploymentLookup);

        var adminReq = new DeploymentAdminRequirement();
        var keyRootReq = new KeyRootAndBootstrapAdminRequirement();

        var adminContext = new AuthorizationHandlerContext([adminReq], principal, null);
        var keyRootContext = new AuthorizationHandlerContext([keyRootReq], principal, null);

        await adminHandler.HandleAsync(adminContext);
        await keyRootHandler.HandleAsync(keyRootContext);

        Assert.False(adminContext.HasSucceeded, "DeploymentAdminHandler must fail when caller has no deployment superadmin grant.");
        Assert.False(keyRootContext.HasSucceeded, "KeyRootAndBootstrapAdminHandler must fail when caller has no deployment superadmin grant.");
    }

    [Fact]
    public void Policies_AreNamedAndRegistered()
    {
        // Req 2: Every initial settings/admin endpoint carries a named policy.
        // Unannotated endpoints fail unless on an explicit anonymous allowlist.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });

        builder.Services.AddRagPipelineAuthorization();
        builder.Services.AddModule<IdentityAccessModule>(builder.Configuration);
        builder.Services.AddModule<TenantManagementModule>(builder.Configuration);
        builder.Services.AddModule<DeploymentCapabilitiesModule>(builder.Configuration);
        builder.Services.AddScoped<ITenantProfileStore, FakeTenantProfileStore>();
        builder.Services.AddScoped<ITenantMembershipLookup, FakeTenantMembershipLookup>();
        builder.Services.AddScoped<ITenantContextResolver, TenantContextResolver>();
        builder.Services.AddScoped<ITenantContextAccessor, FakeTenantContextAccessor>(_ => new FakeTenantContextAccessor(TenantContext.None));
        builder.Services.AddScoped<TenantContextEndpointFilter>();

        var app = builder.Build();
        app.MapModuleEndpoints();

        var endpointSources = ((IEndpointRouteBuilder)app).DataSources;
        var endpoints = endpointSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>().ToList();

        Assert.NotEmpty(endpoints);

        var anonymousAllowlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/api/v1/system/health/live",
            "/api/v1/system/health/ready"
        };

        foreach (var endpoint in endpoints)
        {
            var routePattern = endpoint.RoutePattern.RawText ?? string.Empty;
            var isAllowlistedAnonymous = anonymousAllowlist.Contains(routePattern);
            var allowsAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null;
            var authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToList();

            if (isAllowlistedAnonymous)
            {
                Assert.True(allowsAnonymous, $"Endpoint '{routePattern}' is on anonymous allowlist but does not allow anonymous.");
            }
            else
            {
                Assert.False(allowsAnonymous, $"Endpoint '{routePattern}' allows anonymous but is NOT on the explicit anonymous allowlist.");
                Assert.NotEmpty(authorizeData);

                var policyName = authorizeData.First().Policy;
                Assert.False(string.IsNullOrWhiteSpace(policyName), $"Endpoint '{routePattern}' has an empty policy name.");
                Assert.Contains(policyName, Policies.All);
            }
        }
    }

    private sealed class FakeTenantContextAccessor(ITenantContext tenantContext) : ITenantContextAccessor
    {
        public ITenantContext TenantContext { get; set; } = tenantContext;
    }

    private sealed class FakeDeploymentOwnerLookup(IEnumerable<Guid> superadminUserIds) : IDeploymentOwnerLookup
    {
        private readonly HashSet<Guid> _superadmins = new(superadminUserIds);

        public Task<bool> IsDeploymentSuperadminAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_superadmins.Contains(userId));
        }
    }

    private sealed class FakeTenantProfileStore : ITenantProfileStore
    {
        public Task<TenantProfileDto?> GetProfileAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantProfileDto?>(null);

        public Task<TenantProfileDto> UpdateProfileAsync(Guid tenantId, string displayName, string settingsJson, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TenantProfileDto(tenantId, displayName, settingsJson, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
    }

    private sealed class FakeTenantMembershipLookup : ITenantMembershipLookup
    {
        public Task<TenantMembershipInfo?> FindMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantMembershipInfo?>(null);
    }
}
