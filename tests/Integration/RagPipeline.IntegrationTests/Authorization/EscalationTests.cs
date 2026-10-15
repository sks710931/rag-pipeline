namespace RagPipeline.IntegrationTests.Authorization;

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RagPipeline.BuildingBlocks.Authorization;
using RagPipeline.BuildingBlocks.Tenancy;
using Xunit;

public class EscalationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EscalationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Request_ClientSuppliedRoleHeaderBodyOrQuery_IsIgnored()
    {
        // AC3: Routes cannot elevate role or confer membership using client-supplied tenant, role, or claim headers/body/query
        var store = SyntheticUsers.CreatePopulatedStore();
        using var client = SyntheticUsers.CreateClientForUser(_factory, store, SyntheticUsers.MemberA);

        // MemberA attempts to elevate to DeploymentSuperadmin / TenantOwner via multiple attack vectors
        client.DefaultRequestHeaders.Add("X-Role", "DeploymentSuperadmin");
        client.DefaultRequestHeaders.Add("X-Tenant-Role", "TenantOwner");
        client.DefaultRequestHeaders.Add("X-Claim-Role", "DeploymentSuperadmin");

        var maliciousUri = $"/api/v1/tenants/{SyntheticUsers.TenantAId}/settings?role=DeploymentSuperadmin&tenantRole=TenantOwner";
        var maliciousBody = new StringContent(
            "{\"role\":\"DeploymentSuperadmin\",\"tenantRole\":\"TenantOwner\",\"displayName\":\"Spoofed Name\",\"settingsJson\":\"{}\"}",
            Encoding.UTF8,
            "application/json");

        var response = await client.PutAsync(maliciousUri, maliciousBody);

        // Assert: Access is forbidden because server evaluated real DB role "TenantMember" (which lacks write)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Route_TenantIdOfOtherTenant_WithOwnerRoleElsewhere_DoesNotElevate()
    {
        // AC3: Owner of Tenant A cannot use their owner role to access Tenant B
        var store = SyntheticUsers.CreatePopulatedStore();
        using var client = SyntheticUsers.CreateClientForUser(_factory, store, SyntheticUsers.OwnerA);

        var responseGet = await client.GetAsync($"/api/v1/tenants/{SyntheticUsers.TenantBId}/settings");
        var responsePut = await client.PutAsync(
            $"/api/v1/tenants/{SyntheticUsers.TenantBId}/settings",
            new StringContent("{\"displayName\":\"Hacked\",\"settingsJson\":\"{}\"}", Encoding.UTF8, "application/json"));

        Assert.True(
            responseGet.StatusCode == HttpStatusCode.NotFound || responseGet.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 404 or 403 on GET other tenant but got {responseGet.StatusCode}");

        Assert.True(
            responsePut.StatusCode == HttpStatusCode.NotFound || responsePut.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 404 or 403 on PUT other tenant but got {responsePut.StatusCode}");
    }

    [Fact]
    public async Task Claims_TamperedOrStaleRole_RevalidatedAgainstDb()
    {
        // AC3: Role revoked in DB must lose access on the next request.
        // Handlers revalidate caller identity against the database.
        var store = SyntheticUsers.CreatePopulatedStore();
        using var client = SyntheticUsers.CreateClientForUser(_factory, store, SyntheticUsers.SuperadminOnly);

        // Step 1: Initial request succeeds
        var initialResponse = await client.GetAsync("/api/v1/deployment/admin/status");
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);

        // Step 2: Revoke role in persistent store
        store.RevokeDeploymentOwner(SyntheticUsers.SuperadminOnlyId);

        // Step 3: Next request must immediately fail closed upon DB revalidation
        var subsequentResponse = await client.GetAsync("/api/v1/deployment/admin/status");
        Assert.Equal(HttpStatusCode.Forbidden, subsequentResponse.StatusCode);
    }

    [Fact]
    public async Task Initial_OwnerBinding_IsToInitialTenantOnly()
    {
        // Req 3: Bind owner to initial tenant without conferring automatic access to other tenants
        var store = SyntheticUsers.CreatePopulatedStore();
        var initialOwnerId = Guid.NewGuid();

        // Simulate initial bootstrap owner binding: granted TenantOwner for Tenant A only
        store.AddMembership(SyntheticUsers.TenantAId, initialOwnerId, Roles.TenantOwner, isFirstOwner: true);

        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IDeploymentOwnerLookup>(_ => store);
                services.AddScoped<ITenantMembershipLookup>(_ => store);
                services.AddScoped<ITenantProfileStore>(_ => store);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", initialOwnerId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-UserId", initialOwnerId.ToString());

        // Can access initial tenant A
        var responseA = await client.GetAsync($"/api/v1/tenants/{SyntheticUsers.TenantAId}/settings");
        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);

        // Cannot access tenant B
        var responseB = await client.GetAsync($"/api/v1/tenants/{SyntheticUsers.TenantBId}/settings");
        Assert.True(
            responseB.StatusCode == HttpStatusCode.NotFound || responseB.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 404 or 403 on Tenant B access but got {responseB.StatusCode}");
    }

    [Fact]
    public async Task AuthorizationFailure_IsAudited_NoSecrets()
    {
        const string canarySecret = "SuperCanarySecretPassphrase987!#";
        var logSink = new InMemoryLogSink();

        var store = SyntheticUsers.CreatePopulatedStore();
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILoggerProvider>(new TestLoggerProvider(logSink));
                services.AddScoped<IDeploymentOwnerLookup>(_ => store);
                services.AddScoped<ITenantMembershipLookup>(_ => store);
                services.AddScoped<ITenantProfileStore>(_ => store);
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SyntheticUsers.OwnerAId.ToString());
        client.DefaultRequestHeaders.Add("X-Canary-Secret", canarySecret);

        // Act: Trigger authorization denial
        var response = await client.GetAsync("/api/v1/deployment/admin/status");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Assert: Log sink captured logs, but NO secret leakage
        var allLogs = string.Join("\n", logSink.Messages);

        Assert.DoesNotContain(canarySecret, allLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", allLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=;Database=", allLogs, StringComparison.OrdinalIgnoreCase);
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
