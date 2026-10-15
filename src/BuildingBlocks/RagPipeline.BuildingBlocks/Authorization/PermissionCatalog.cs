namespace RagPipeline.BuildingBlocks.Authorization;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

public static class DeploymentPermissions
{
    public const string Admin = "deployment:admin";
    public const string BootstrapRead = "deployment:bootstrap:read";
    public const string BootstrapSeal = "deployment:bootstrap:seal";
    public const string KeyRootAdmin = "deployment:keyroot:admin";
    public const string AuditRead = "deployment:audit:read";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Admin,
        BootstrapRead,
        BootstrapSeal,
        KeyRootAdmin,
        AuditRead
    }.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
}

public static class TenantPermissions
{
    public const string SettingsRead = "tenant:settings:read";
    public const string SettingsWrite = "tenant:settings:write";
    public const string ProfileRead = "tenant:profile:read";
    public const string ProfileWrite = "tenant:profile:write";
    public const string MembersManage = "tenant:members:manage";
    public const string AuditRead = "tenant:audit:read";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SettingsRead,
        SettingsWrite,
        ProfileRead,
        ProfileWrite,
        MembersManage,
        AuditRead
    }.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
}

public static class DocumentPermissions
{
    public const string DocumentRead = "document:read";
    public const string DocumentWrite = "document:write";
    public const string ContentClassifiedRead = "content:classified:read";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        DocumentRead,
        DocumentWrite,
        ContentClassifiedRead
    }.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
}

public static class Roles
{
    public const string DeploymentSuperadmin = "DeploymentSuperadmin";
    public const string TenantOwner = "TenantOwner";
    public const string TenantAdmin = "TenantAdmin";
    public const string TenantMember = "TenantMember";
}

public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RoleMap =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Roles.DeploymentSuperadmin] = DeploymentPermissions.All,
            [Roles.TenantOwner] = TenantPermissions.All,
            [Roles.TenantAdmin] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                TenantPermissions.SettingsRead,
                TenantPermissions.SettingsWrite,
                TenantPermissions.ProfileRead,
                TenantPermissions.ProfileWrite
            }.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase),
            [Roles.TenantMember] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                TenantPermissions.SettingsRead,
                TenantPermissions.ProfileRead
            }.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase)
        };

    public static IReadOnlySet<string> GetPermissions(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return ImmutableHashSet<string>.Empty;
        }

        return RoleMap.TryGetValue(role, out var permissions)
            ? permissions
            : ImmutableHashSet<string>.Empty;
    }
}

public static class Policies
{
    public const string DeploymentAdmin = "DeploymentAdmin";
    public const string KeyRootAndBootstrapAdmin = "KeyRootAndBootstrapAdmin";
    public const string TenantSettingsRead = "TenantSettingsRead";
    public const string TenantSettingsWrite = "TenantSettingsWrite";
    public const string TenantOwner = "TenantOwner";
    public const string TenantAuditRead = "TenantAuditRead";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        DeploymentAdmin,
        KeyRootAndBootstrapAdmin,
        TenantSettingsRead,
        TenantSettingsWrite,
        TenantOwner,
        TenantAuditRead
    }.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
}
