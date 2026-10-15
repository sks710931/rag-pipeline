namespace RagPipeline.IntegrationTests.Audit;

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using RagPipeline.BuildingBlocks.Audit;
using RagPipeline.IntegrationTests.Authorization;
using Xunit;

public class AuditApiAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuditApiAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(SyntheticUsers.OwnerA, "A", 200)]
    [InlineData(SyntheticUsers.OwnerA, "B", 404)]
    [InlineData(SyntheticUsers.MemberA, "A", 403)]
    [InlineData(SyntheticUsers.MemberA, "B", 404)]
    [InlineData(SyntheticUsers.SuperadminOnly, "A", 404)]
    [InlineData(SyntheticUsers.SuperadminOnly, "B", 404)]
    [InlineData(SyntheticUsers.Anonymous, "A", 401)]
    [InlineData(SyntheticUsers.Anonymous, "B", 401)]
    public async Task AuditApi_ReadRequiresLeastPrivilegePolicy(string user, string tenant, int expectedStatusCode)
    {
        var store = SyntheticUsers.CreatePopulatedStore();
        var tenantAId = SyntheticUsers.TenantAId;
        var tenantBId = SyntheticUsers.TenantBId;
        var tenantId = tenant == "A" ? tenantAId : tenantBId;

        // Seed some audit events for tenant A
        await store.AppendAsync(AuditEvent.Create(
            actorType: "User",
            action: "Tenant.Settings.Updated",
            outcome: "Success",
            correlationId: "corr-1",
            occurredAt: DateTimeOffset.UtcNow,
            tenantId: tenantAId));

        using var client = SyntheticUsers.CreateClientForUser(_factory, store, user);
        var uri = $"/api/v1/tenants/{tenantId}/audit?page=1&pageSize=10";

        var response = await client.GetAsync(uri);

        if (expectedStatusCode == 404)
        {
            // Fail-closed non-member access returns 404/403 without disclosing tenant existence
            Assert.True(
                response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden,
                $"Expected 404 or 403 but got {response.StatusCode} for user {user} on tenant {tenant}");
        }
        else
        {
            Assert.Equal((HttpStatusCode)expectedStatusCode, response.StatusCode);
        }
    }

    [Fact]
    public async Task AuditApi_PlatformOwnerWithoutMembership_CannotReadTenantContentClassifiedFields()
    {
        var store = SyntheticUsers.CreatePopulatedStore();
        var tenantAId = SyntheticUsers.TenantAId;

        await store.AppendAsync(AuditEvent.Create(
            actorType: "User",
            action: "Document.Classified.AccessAttempted",
            outcome: "Denied",
            correlationId: "corr-classified",
            occurredAt: DateTimeOffset.UtcNow,
            tenantId: tenantAId,
            detailsJson: "{\"classifiedLevel\":\"Restricted\"}"));

        using var client = SyntheticUsers.CreateClientForUser(_factory, store, SyntheticUsers.SuperadminOnly);
        var uri = $"/api/v1/tenants/{tenantAId}/audit";

        var response = await client.GetAsync(uri);

        // Platform superadmin without tenant membership cannot access tenant audit events (AC2)
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 404/403 but got {response.StatusCode}");
    }

    [Fact]
    public async Task AuditApi_Pagination_CannotBeUsedToEnumerateOtherTenants()
    {
        var store = SyntheticUsers.CreatePopulatedStore();
        var tenantAId = SyntheticUsers.TenantAId;
        var tenantBId = SyntheticUsers.TenantBId;

        // Seed events for both tenants
        await store.AppendAsync(AuditEvent.Create(
            actorType: "User",
            action: "Tenant.Settings.Updated",
            outcome: "Success",
            correlationId: "corr-a",
            occurredAt: DateTimeOffset.UtcNow,
            tenantId: tenantAId));

        await store.AppendAsync(AuditEvent.Create(
            actorType: "User",
            action: "Tenant.Secret.Updated",
            outcome: "Success",
            correlationId: "corr-b",
            occurredAt: DateTimeOffset.UtcNow,
            tenantId: tenantBId));

        using var client = SyntheticUsers.CreateClientForUser(_factory, store, SyntheticUsers.OwnerA);

        // Attempt to supply tenantId in query string to cross-enumerate tenant B while calling route A
        var uri = $"/api/v1/tenants/{tenantAId}/audit?tenantId={tenantBId}&page=1&pageSize=10";

        var response = await client.GetAsync(uri);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement.GetProperty("items");

        // Every returned event MUST belong strictly to Tenant A, never Tenant B
        foreach (var item in items.EnumerateArray())
        {
            var eventTenantId = item.GetProperty("tenantId").GetString();
            Assert.Equal(tenantAId.ToString(), eventTenantId);
            Assert.NotEqual(tenantBId.ToString(), eventTenantId);
        }
    }
}
