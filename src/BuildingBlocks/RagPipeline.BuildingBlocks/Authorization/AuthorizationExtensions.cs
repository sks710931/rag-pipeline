namespace RagPipeline.BuildingBlocks.Authorization;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddRagPipelineAuthorization(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = FailClosedAuthenticationHandler.SchemeName;
            options.DefaultChallengeScheme = FailClosedAuthenticationHandler.SchemeName;
            options.DefaultForbidScheme = FailClosedAuthenticationHandler.SchemeName;
        })
        .AddScheme<AuthenticationSchemeOptions, FailClosedAuthenticationHandler>(FailClosedAuthenticationHandler.SchemeName, _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.DeploymentAdmin, policy =>
                policy.RequireAuthenticatedUser()
                      .AddRequirements(new DeploymentAdminRequirement()));

            options.AddPolicy(Policies.KeyRootAndBootstrapAdmin, policy =>
                policy.RequireAuthenticatedUser()
                      .AddRequirements(new KeyRootAndBootstrapAdminRequirement()));

            options.AddPolicy(Policies.TenantSettingsRead, policy =>
                policy.RequireAuthenticatedUser()
                      .AddRequirements(new TenantPermissionRequirement(TenantPermissions.SettingsRead)));

            options.AddPolicy(Policies.TenantSettingsWrite, policy =>
                policy.RequireAuthenticatedUser()
                      .AddRequirements(new TenantPermissionRequirement(TenantPermissions.SettingsWrite)));

            options.AddPolicy(Policies.TenantOwner, policy =>
                policy.RequireAuthenticatedUser()
                      .AddRequirements(new TenantRoleRequirement(Roles.TenantOwner)));

            options.AddPolicy(Policies.TenantAuditRead, policy =>
                policy.RequireAuthenticatedUser()
                      .AddRequirements(new TenantPermissionRequirement(TenantPermissions.AuditRead)));
        });

        services.AddScoped<IAuthorizationHandler, DeploymentAdminHandler>();
        services.AddScoped<IAuthorizationHandler, KeyRootAndBootstrapAdminHandler>();
        services.AddScoped<IAuthorizationHandler, TenantPermissionHandler>();
        services.AddScoped<IAuthorizationHandler, TenantRoleHandler>();

        return services;
    }
}
