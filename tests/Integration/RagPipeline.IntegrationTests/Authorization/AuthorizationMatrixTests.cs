namespace RagPipeline.IntegrationTests.Authorization;

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using RagPipeline.BuildingBlocks.ErrorHandling;
using Xunit;

public class AuthorizationMatrixTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthorizationMatrixTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(SyntheticUsers.OwnerA, "A", "GET", 200)]
    [InlineData(SyntheticUsers.OwnerA, "A", "PUT", 200)]
    [InlineData(SyntheticUsers.OwnerA, "B", "GET", 404)]
    [InlineData(SyntheticUsers.OwnerA, "B", "PUT", 404)]
    [InlineData(SyntheticUsers.SuperadminOnly, "A", "GET", 404)]
    [InlineData(SyntheticUsers.SuperadminOnly, "A", "PUT", 404)]
    [InlineData(SyntheticUsers.SuperadminOnly, "B", "GET", 404)]
    [InlineData(SyntheticUsers.SuperadminOnly, "B", "PUT", 404)]
    [InlineData(SyntheticUsers.MemberA, "A", "GET", 200)]
    [InlineData(SyntheticUsers.MemberA, "A", "PUT", 403)]
    [InlineData(SyntheticUsers.Anonymous, "A", "GET", 401)]
    [InlineData(SyntheticUsers.Anonymous, "A", "PUT", 401)]
    public async Task Matrix_TenantSettings_ReadWrite(string user, string tenant, string method, int expectedStatusCode)
    {
        var store = SyntheticUsers.CreatePopulatedStore();
        using var client = SyntheticUsers.CreateClientForUser(_factory, store, user);

        var tenantId = tenant == "A" ? SyntheticUsers.TenantAId : SyntheticUsers.TenantBId;
        var uri = $"/api/v1/tenants/{tenantId}/settings";

        HttpResponseMessage response;
        if (method == "GET")
        {
            response = await client.GetAsync(uri);
        }
        else
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { displayName = "Updated Display Name", settingsJson = "{\"theme\":\"custom\"}" }),
                Encoding.UTF8,
                "application/json");
            response = await client.PutAsync(uri, content);
        }

        if (expectedStatusCode == 404)
        {
            // Per SENTINEL spec: non-member access returns 403/404 fail-closed without leaking tenant existence
            Assert.True(
                response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden,
                $"Expected 404 or 403 but got {response.StatusCode} for user {user} on tenant {tenant} {method}");
        }
        else
        {
            Assert.Equal((HttpStatusCode)expectedStatusCode, response.StatusCode);
        }
    }

    [Theory]
    [InlineData(SyntheticUsers.SuperadminOnly, "/api/v1/deployment/admin/status", 200)]
    [InlineData(SyntheticUsers.OwnerA, "/api/v1/deployment/admin/status", 403)]
    [InlineData(SyntheticUsers.Anonymous, "/api/v1/deployment/admin/status", 401)]
    public async Task Matrix_DeploymentAdminEndpoints(string user, string endpoint, int expectedStatusCode)
    {
        var store = SyntheticUsers.CreatePopulatedStore();
        using var client = SyntheticUsers.CreateClientForUser(_factory, store, user);

        var response = await client.GetAsync(endpoint);

        Assert.Equal((HttpStatusCode)expectedStatusCode, response.StatusCode);
    }

    [Theory]
    [InlineData(SyntheticUsers.SuperadminOnly, 200)]
    [InlineData(SyntheticUsers.OwnerA, 403)]
    [InlineData(SyntheticUsers.Anonymous, 401)]
    public async Task Matrix_KeyRootAndBootstrapAdmin_DeniedToTenantOwner(string user, int expectedStatusCode)
    {
        // AC2: Key/root and bootstrap administration is denied to Tenant Owner
        var store = SyntheticUsers.CreatePopulatedStore();
        using var client = SyntheticUsers.CreateClientForUser(_factory, store, user);

        var response = await client.GetAsync("/api/v1/deployment/admin/keys/root");
        Assert.Equal((HttpStatusCode)expectedStatusCode, response.StatusCode);
    }

    [Fact]
    public async Task SuperadminWithExplicitMembership_CanReadOwnTenantOnly()
    {
        // AC1: Platform Superadmin with explicit membership in Tenant A can read Tenant A,
        // but cannot access Tenant B without an explicit grant.
        var store = SyntheticUsers.CreatePopulatedStore();
        using var client = SyntheticUsers.CreateClientForUser(_factory, store, SyntheticUsers.SuperadminAndOwnerA);

        // Can reach deployment admin
        var adminResponse = await client.GetAsync("/api/v1/deployment/admin/status");
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);

        // Can reach Tenant A settings (explicit membership)
        var tenantAResponse = await client.GetAsync($"/api/v1/tenants/{SyntheticUsers.TenantAId}/settings");
        Assert.Equal(HttpStatusCode.OK, tenantAResponse.StatusCode);

        // Cannot reach Tenant B settings (no membership in Tenant B)
        var tenantBResponse = await client.GetAsync($"/api/v1/tenants/{SyntheticUsers.TenantBId}/settings");
        Assert.True(
            tenantBResponse.StatusCode == HttpStatusCode.NotFound || tenantBResponse.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 404 or 403 for Tenant B access, but got {tenantBResponse.StatusCode}");
    }

    [Theory]
    [InlineData(SyntheticUsers.Anonymous, "/api/v1/deployment/admin/status", "GET")]
    [InlineData(SyntheticUsers.OwnerA, "/api/v1/deployment/admin/status", "GET")]
    [InlineData(SyntheticUsers.OwnerA, "/api/v1/deployment/admin/keys/root", "GET")]
    [InlineData(SyntheticUsers.MemberA, "/api/v1/tenants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/settings", "PUT")]
    public async Task Denials_ReturnSafeProblemDetails(string user, string endpoint, string method)
    {
        var store = SyntheticUsers.CreatePopulatedStore();
        using var client = SyntheticUsers.CreateClientForUser(_factory, store, user);

        HttpResponseMessage response;
        if (method == "GET")
        {
            response = await client.GetAsync(endpoint);
        }
        else
        {
            response = await client.PutAsync(endpoint, new StringContent("{\"displayName\":\"Fail\",\"settingsJson\":\"{}\"}", Encoding.UTF8, "application/json"));
        }

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected denial status code but got {response.StatusCode}");

        Assert.Equal(ProblemDetailsExtensions.ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("status", out var statusProp));
        Assert.Equal((int)response.StatusCode, statusProp.GetInt32());

        Assert.True(root.TryGetProperty("title", out var titleProp));
        Assert.False(string.IsNullOrWhiteSpace(titleProp.GetString()));

        Assert.True(root.TryGetProperty("traceId", out var traceIdProp));
        Assert.False(string.IsNullOrWhiteSpace(traceIdProp.GetString()));

        // Ensure no internal implementation details, policy names, or sensitive role names leak
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DeploymentSuperadmin", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DeploymentAdmin", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("KeyRootAndBootstrapAdmin", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=;Database=", body, StringComparison.OrdinalIgnoreCase);
    }
}
