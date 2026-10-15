namespace RagPipeline.IntegrationTests.Authorization;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RagPipeline.BuildingBlocks.Authorization;
using RagPipeline.BuildingBlocks.Tenancy;

public static class SyntheticUsers
{
    public static readonly Guid SuperadminOnlyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OwnerAId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid OwnerBId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid MemberAId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid SuperadminAndOwnerAId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid AnonymousId = Guid.Empty;

    public static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public const string SuperadminOnly = "SuperadminOnly";
    public const string OwnerA = "OwnerA";
    public const string OwnerB = "OwnerB";
    public const string MemberA = "MemberA";
    public const string SuperadminAndOwnerA = "SuperadminAndOwnerA";
    public const string Anonymous = "Anonymous";

    public static Guid GetUserId(string user) => user switch
    {
        SuperadminOnly => SuperadminOnlyId,
        OwnerA => OwnerAId,
        OwnerB => OwnerBId,
        MemberA => MemberAId,
        SuperadminAndOwnerA => SuperadminAndOwnerAId,
        Anonymous => AnonymousId,
        _ => throw new ArgumentException($"Unknown synthetic user: {user}", nameof(user))
    };

    public static InMemoryAuthzTestStore CreatePopulatedStore()
    {
        var store = new InMemoryAuthzTestStore();

        // 1. Tenants profiles
        store.AddProfile(new TenantProfileDto(TenantAId, "Tenant Alpha", "{\"theme\":\"dark\"}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        store.AddProfile(new TenantProfileDto(TenantBId, "Tenant Beta", "{\"theme\":\"light\"}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        // 2. Deployment Owners
        store.AddDeploymentOwner(SuperadminOnlyId);
        store.AddDeploymentOwner(SuperadminAndOwnerAId);

        // 3. Tenant Memberships
        store.AddMembership(TenantAId, OwnerAId, Roles.TenantOwner, isFirstOwner: true);
        store.AddMembership(TenantBId, OwnerBId, Roles.TenantOwner, isFirstOwner: true);
        store.AddMembership(TenantAId, MemberAId, Roles.TenantMember, isFirstOwner: false);
        store.AddMembership(TenantAId, SuperadminAndOwnerAId, Roles.TenantOwner, isFirstOwner: false);

        return store;
    }

    public static HttpClient CreateClientForUser(
        WebApplicationFactory<Program> factory,
        InMemoryAuthzTestStore store,
        string user)
    {
        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IDeploymentOwnerLookup>(_ => store);
                services.AddScoped<ITenantMembershipLookup>(_ => store);
                services.AddScoped<ITenantProfileStore>(_ => store);
                services.AddScoped<RagPipeline.BuildingBlocks.Audit.IAuditEventStore>(_ => store);

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        });

        var client = customFactory.CreateClient();

        if (user != Anonymous)
        {
            var userId = GetUserId(user);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        }

        return client;
    }
}

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue("X-Test-Anonymous", out var isAnon) &&
            string.Equals(isAnon, "true", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Guid userId = Guid.Empty;

        var authHeader = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            var parts = authHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2 &&
                (string.Equals(parts[0], "Bearer", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[0], "Test", StringComparison.OrdinalIgnoreCase)))
            {
                _ = Guid.TryParse(parts[1], out userId);
            }
        }

        if (userId == Guid.Empty &&
            Request.Headers.TryGetValue("X-Test-UserId", out var headerUserId) &&
            Guid.TryParse(headerUserId, out var parsedHeaderId))
        {
            userId = parsedHeaderId;
        }

        if (userId == Guid.Empty)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"user-{userId:N}")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryAuthzTestStore : IDeploymentOwnerLookup, ITenantMembershipLookup, ITenantProfileStore, RagPipeline.BuildingBlocks.Audit.IAuditEventStore
{
    private readonly ConcurrentDictionary<Guid, bool> _deploymentSuperadmins = new();
    private readonly ConcurrentDictionary<(Guid TenantId, Guid UserId), (string Role, bool IsFirstOwner)> _memberships = new();
    private readonly ConcurrentDictionary<Guid, TenantProfileDto> _profiles = new();
    private readonly ConcurrentBag<RagPipeline.BuildingBlocks.Audit.AuditEvent> _auditEvents = new();

    public void AddDeploymentOwner(Guid userId)
    {
        _deploymentSuperadmins[userId] = true;
    }

    public void RevokeDeploymentOwner(Guid userId)
    {
        _deploymentSuperadmins.TryRemove(userId, out _);
    }

    public void AddMembership(Guid tenantId, Guid userId, string role, bool isFirstOwner = false)
    {
        _memberships[(tenantId, userId)] = (role, isFirstOwner);
    }

    public void RevokeMembership(Guid tenantId, Guid userId)
    {
        _memberships.TryRemove((tenantId, userId), out _);
    }

    public void AddProfile(TenantProfileDto profile)
    {
        _profiles[profile.TenantId] = profile;
    }

    public Task<bool> IsDeploymentSuperadminAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_deploymentSuperadmins.TryGetValue(userId, out var isSuperadmin) && isSuperadmin);
    }

    public Task<TenantMembershipInfo?> FindMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (_memberships.TryGetValue((tenantId, userId), out var info))
        {
            return Task.FromResult<TenantMembershipInfo?>(new TenantMembershipInfo(tenantId, userId, info.Role, info.IsFirstOwner));
        }

        return Task.FromResult<TenantMembershipInfo?>(null);
    }

    public Task<TenantProfileDto?> GetProfileAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _profiles.TryGetValue(tenantId, out var profile);
        return Task.FromResult(profile);
    }

    public Task<TenantProfileDto> UpdateProfileAsync(Guid tenantId, string displayName, string settingsJson, CancellationToken cancellationToken = default)
    {
        var updated = new TenantProfileDto(tenantId, displayName, settingsJson, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _profiles[tenantId] = updated;
        return Task.FromResult(updated);
    }

    public Task AppendAsync(RagPipeline.BuildingBlocks.Audit.AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        _auditEvents.Add(auditEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RagPipeline.BuildingBlocks.Audit.AuditEvent>> GetTenantEventsAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var events = _auditEvents.Where(e => e.TenantId == tenantId).OrderByDescending(e => e.OccurredAt).Skip(skip).Take(take).ToList();
        return Task.FromResult<IReadOnlyList<RagPipeline.BuildingBlocks.Audit.AuditEvent>>(events);
    }

    public Task<int> GetTenantEventsCountAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_auditEvents.Count(e => e.TenantId == tenantId));
    }
}
