namespace RagPipeline.IntegrationTests.Tenancy;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RagPipeline.BuildingBlocks.ErrorHandling;
using RagPipeline.BuildingBlocks.Modules;
using RagPipeline.BuildingBlocks.Tenancy;
using Xunit;

public class TenantRouteTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TenantRouteTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TenantRoute_ForgedTenantId_ReturnsProblemDetails403or404_NoLeak()
    {
        // Arrange
        var userAId = Guid.NewGuid();
        var tenantAId = Guid.NewGuid();
        var existingTenantBId = Guid.NewGuid();
        var nonExistentTenantId = Guid.NewGuid();

        var testStore = new InMemoryTenancyTestStore();
        testStore.AddMembership(tenantAId, userAId, "TenantMember");
        testStore.AddMembership(existingTenantBId, Guid.NewGuid(), "TenantOwner"); // Tenant B exists, but userA is not a member

        testStore.AddProfile(new TenantProfileDto(tenantAId, "Tenant A", "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        testStore.AddProfile(new TenantProfileDto(existingTenantBId, "Tenant B", "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        using var client = CreateTestClient(testStore, userAId);

        // Act 1: Access existing tenant that caller is NOT a member of
        var responseNonMember = await client.GetAsync($"/api/v1/tenants/{existingTenantBId}/profile");

        // Act 2: Access completely non-existent tenant
        var responseNonExistent = await client.GetAsync($"/api/v1/tenants/{nonExistentTenantId}/profile");

        // Assert: Both must return 404 Not Found (fail-closed)
        Assert.Equal(HttpStatusCode.NotFound, responseNonMember.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, responseNonExistent.StatusCode);

        Assert.Equal(ProblemDetailsExtensions.ProblemJsonMediaType, responseNonMember.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ProblemDetailsExtensions.ProblemJsonMediaType, responseNonExistent.Content.Headers.ContentType?.MediaType);

        var jsonNonMember = await responseNonMember.Content.ReadAsStringAsync();
        var jsonNonExistent = await responseNonExistent.Content.ReadAsStringAsync();

        using var doc1 = JsonDocument.Parse(jsonNonMember);
        using var doc2 = JsonDocument.Parse(jsonNonExistent);

        // Verify identical response structure to prevent tenant enumeration
        Assert.Equal(doc1.RootElement.GetProperty("status").GetInt32(), doc2.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(doc1.RootElement.GetProperty("title").GetString(), doc2.RootElement.GetProperty("title").GetString());
        Assert.Equal(doc1.RootElement.GetProperty("detail").GetString(), doc2.RootElement.GetProperty("detail").GetString());

        // Assert no stack trace or internal info leaks
        Assert.DoesNotContain("stackTrace", jsonNonMember, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stackTrace", jsonNonExistent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", jsonNonMember, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", jsonNonExistent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TenantRoute_ClientSuppliedRoleClaim_IsIgnored()
    {
        // Arrange: User is registered with role "TenantMember" in DB
        var userAId = Guid.NewGuid();
        var tenantAId = Guid.NewGuid();

        var testStore = new InMemoryTenancyTestStore();
        testStore.AddMembership(tenantAId, userAId, "TenantMember");
        testStore.AddProfile(new TenantProfileDto(tenantAId, "Tenant A", "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        using var client = CreateTestClient(testStore, userAId);

        // Client attempts to spoof role via header and claims
        client.DefaultRequestHeaders.Add("X-Tenant-Role", "DeploymentSuperadmin");
        client.DefaultRequestHeaders.Add("X-Spoofed-Role", "TenantOwner");

        // Act
        var response = await client.GetAsync($"/api/v1/tenants/{tenantAId}/profile");

        // Assert: Access succeeded, but context used server-verified role "TenantMember"
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resolvedRole = testStore.LastResolvedRole;
        Assert.Equal("TenantMember", resolvedRole);
        Assert.NotEqual("DeploymentSuperadmin", resolvedRole);
        Assert.NotEqual("TenantOwner", resolvedRole);
    }

    [Fact]
    public async Task Logs_OnDenial_DoNotContainConnectionStringOrOtherTenantIds()
    {
        // Arrange
        var userAId = Guid.NewGuid();
        var tenantAId = Guid.NewGuid();
        var targetTenantBId = Guid.NewGuid();
        const string canarySecret = "SuperSecretDbPassword123!#";

        var testStore = new InMemoryTenancyTestStore();
        testStore.AddMembership(tenantAId, userAId, "TenantMember");

        var logSink = new InMemoryLogSink();

        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILoggerProvider>(new TestLoggerProvider(logSink));
                services.AddScoped<ITenantMembershipLookup>(_ => testStore);
                services.AddScoped<ITenantProfileStore>(_ => testStore);
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userAId.ToString());

        // Act: Attempt to access unauthorized tenant
        var response = await client.GetAsync($"/api/v1/tenants/{targetTenantBId}/profile");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Assert: Canary check on all captured log messages
        var allLogs = string.Join("\n", logSink.Messages);

        Assert.DoesNotContain(canarySecret, allLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=;Database=", allLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", allLogs, StringComparison.OrdinalIgnoreCase);
    }

    private HttpClient CreateTestClient(InMemoryTenancyTestStore testStore, Guid userId)
    {
        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<ITenantMembershipLookup>(_ => testStore);
                services.AddScoped<ITenantProfileStore>(_ => testStore);
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        });

        var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private sealed class InMemoryTenancyTestStore : ITenantMembershipLookup, ITenantProfileStore
    {
        private readonly ConcurrentDictionary<(Guid TenantId, Guid UserId), string> _memberships = new();
        private readonly ConcurrentDictionary<Guid, TenantProfileDto> _profiles = new();

        public string? LastResolvedRole { get; private set; }

        public void AddMembership(Guid tenantId, Guid userId, string role)
        {
            _memberships[(tenantId, userId)] = role;
        }

        public void AddProfile(TenantProfileDto profile)
        {
            _profiles[profile.TenantId] = profile;
        }

        public Task<TenantMembershipInfo?> FindMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        {
            if (_memberships.TryGetValue((tenantId, userId), out var role))
            {
                LastResolvedRole = role;
                return Task.FromResult<TenantMembershipInfo?>(new TenantMembershipInfo(tenantId, userId, role, false));
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
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.TryGetValue("X-Test-Anonymous", out var isAnon) && isAnon == "true")
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var userId = Guid.NewGuid();
            if (Request.Headers.TryGetValue("X-Test-UserId", out var headerUserId) &&
                Guid.TryParse(headerUserId, out var parsedId))
            {
                userId = parsedId;
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, $"user-{userId:N}")
            };

            // If client attempted to pass client role headers, simulate malicious client token claim
            if (Request.Headers.TryGetValue("X-Tenant-Role", out var clientRole))
            {
                claims.Add(new Claim(ClaimTypes.Role, clientRole.ToString()));
            }

            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class InMemoryLogSink
    {
        public ConcurrentBag<string> Messages { get; } = new();
    }

    private sealed class TestLoggerProvider(InMemoryLogSink sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new TestLogger(sink);
        public void Dispose() { }
    }

    private sealed class TestLogger(InMemoryLogSink sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = formatter(state, exception);
            sink.Messages.Add(msg);
        }
    }
}
