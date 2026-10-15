namespace RagPipeline.UnitTests.Tenancy;

using System;
using System.Data.Common;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RagPipeline.BuildingBlocks.Tenancy;
using RagPipeline.Persistence;
using RagPipeline.Persistence.Interceptors;
using Xunit;

public class TenantContextResolverTests
{
    private readonly InMemoryMembershipLookup _membershipLookup = new();
    private readonly TenantContextAccessor _contextAccessor = new();

    [Fact]
    public async Task TenantContextResolver_MemberOfRouteTenant_ResolvesTenantAndRole()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var expectedRole = "TenantAdmin";

        _membershipLookup.AddMembership(tenantId, userId, expectedRole);

        var resolver = new TenantContextResolver(_membershipLookup, _contextAccessor);
        var httpContext = CreateHttpContext(userId, tenantId);

        // Act
        var result = await resolver.ResolveAsync(httpContext);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Context);
        Assert.Equal(tenantId, result.Context.TenantId);
        Assert.Equal(expectedRole, result.Context.TenantRole);
        Assert.Equal(tenantId, _contextAccessor.TenantContext.TenantId);
        Assert.Equal(expectedRole, _contextAccessor.TenantContext.TenantRole);
    }

    [Fact]
    public async Task TenantContextResolver_NoMembership_Denies()
    {
        // Arrange: User exists, but is not a member of the requested route tenant
        var routeTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _membershipLookup.AddMembership(otherTenantId, userId, "TenantMember");

        var resolver = new TenantContextResolver(_membershipLookup, _contextAccessor);
        var httpContext = CreateHttpContext(userId, routeTenantId);

        // Act
        var result = await resolver.ResolveAsync(httpContext);

        // Assert: Fails closed with 404 Not Found to prevent tenant enumeration
        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Null(result.Context);
        Assert.False(_contextAccessor.TenantContext.HasResolvedTenant);
    }

    [Fact]
    public async Task TenantContextResolver_IgnoresBodyHeaderAndQueryTenantId()
    {
        // Arrange: Route is Tenant A, but Header/Query/Body specify Tenant B
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // User belongs to both Tenant A and Tenant B
        _membershipLookup.AddMembership(tenantA, userId, "TenantAdmin");
        _membershipLookup.AddMembership(tenantB, userId, "TenantOwner");

        var resolver = new TenantContextResolver(_membershipLookup, _contextAccessor);
        var httpContext = CreateHttpContext(userId, tenantA);

        // Inject Tenant B into header, query string, and body
        httpContext.Request.Headers["X-Tenant-Id"] = tenantB.ToString();
        httpContext.Request.Headers["X-Tenant-Role"] = "Superhacker";
        httpContext.Request.QueryString = new QueryString($"?tenantId={tenantB}");

        var bodyJson = $"{{\"tenantId\":\"{tenantB}\",\"role\":\"Superhacker\"}}";
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(bodyJson));

        // Act
        var result = await resolver.ResolveAsync(httpContext);

        // Assert: Resolved tenant must strictly be Tenant A, with role from Tenant A's membership
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Context);
        Assert.Equal(tenantA, result.Context.TenantId);
        Assert.NotEqual(tenantB, result.Context.TenantId);
        Assert.Equal("TenantAdmin", result.Context.TenantRole);
        Assert.NotEqual("TenantOwner", result.Context.TenantRole);
        Assert.NotEqual("Superhacker", result.Context.TenantRole);
    }

    [Fact]
    public async Task TenantContextResolver_Anonymous_Denies()
    {
        // Arrange: Unauthenticated caller
        var tenantId = Guid.NewGuid();
        var resolver = new TenantContextResolver(_membershipLookup, _contextAccessor);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["tenantId"] = tenantId.ToString();
        // User identity is not authenticated

        // Act
        var result = await resolver.ResolveAsync(httpContext);

        // Assert: Denies access
        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Null(result.Context);
        Assert.False(_contextAccessor.TenantContext.HasResolvedTenant);
    }

    [Fact]
    public async Task TenantContext_NotStoredInSessionOrCookie()
    {
        // Arrange: Simulate two concurrent requests for Tenant A and Tenant B on separate execution contexts
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _membershipLookup.AddMembership(tenantA, userId, "TenantAdmin");
        _membershipLookup.AddMembership(tenantB, userId, "TenantMember");

        var resolver = new TenantContextResolver(_membershipLookup, _contextAccessor);

        var httpContextA = CreateHttpContext(userId, tenantA);
        var httpContextB = CreateHttpContext(userId, tenantB);

        var barrier = new SemaphoreSlim(0, 2);
        ITenantContext? observedInTaskA = null;
        ITenantContext? observedInTaskB = null;

        // Act: Run concurrently representing two browser tabs with independent request scopes
        var taskA = Task.Run(async () =>
        {
            var accessorA = new TenantContextAccessor();
            var resolverA = new TenantContextResolver(_membershipLookup, accessorA);

            var res = await resolverA.ResolveAsync(httpContextA);
            Assert.True(res.IsSuccess);
            barrier.Release();
            await Task.Delay(50); // Hold context
            observedInTaskA = accessorA.TenantContext;
        });

        var taskB = Task.Run(async () =>
        {
            await barrier.WaitAsync(); // Ensure taskA has started and resolved
            var accessorB = new TenantContextAccessor();
            var resolverB = new TenantContextResolver(_membershipLookup, accessorB);

            var res = await resolverB.ResolveAsync(httpContextB);
            Assert.True(res.IsSuccess);
            observedInTaskB = accessorB.TenantContext;
        });

        await Task.WhenAll(taskA, taskB);

        // Assert: Each request retains its isolated tenant context without cross-tab bleeding
        Assert.NotNull(observedInTaskA);
        Assert.NotNull(observedInTaskB);
        Assert.Equal(tenantA, observedInTaskA.TenantId);
        Assert.Equal(tenantB, observedInTaskB.TenantId);
        Assert.Equal("TenantAdmin", observedInTaskA.TenantRole);
        Assert.Equal("TenantMember", observedInTaskB.TenantRole);

        // Assert: Neither request set any cookie or session value
        Assert.Equal(0, httpContextA.Response.Headers.SetCookie.Count);
        Assert.Equal(0, httpContextB.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public void TenantScopedDbContext_NoTenantTransaction_Throws()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<RagPipelineDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy;Username=dummy;Password=dummy")
            .Options;

        using var dbContext = new RagPipelineDbContext(options);

        // Verify ActiveTenantTransactionId is initially null
        Assert.Null(dbContext.ActiveTenantTransactionId);

        var tenantCommand = new TestDbCommand
        {
            CommandText = "SELECT * FROM tenant_profiles WHERE tenant_id = @p0"
        };

        var globalCommand = new TestDbCommand
        {
            CommandText = "SELECT * FROM asp_net_users WHERE id = @p0"
        };

        // Act & Assert: Tenant SQL without active tenant transaction throws InvalidOperationException
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TenantSecurityCommandInterceptor.ValidateTenantAccess(tenantCommand, dbContext));

        Assert.Contains("outside a tenant transaction", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Global SQL does not throw even without active tenant transaction
        TenantSecurityCommandInterceptor.ValidateTenantAccess(globalCommand, dbContext);
    }

    private static HttpContext CreateHttpContext(Guid userId, Guid tenantId)
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            "TestAuth");
        context.User = new ClaimsPrincipal(identity);
        context.Request.RouteValues["tenantId"] = tenantId.ToString();
        return context;
    }

    private sealed class InMemoryMembershipLookup : ITenantMembershipLookup
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<(Guid TenantId, Guid UserId), string> _memberships = new();

        public void AddMembership(Guid tenantId, Guid userId, string role)
        {
            _memberships[(tenantId, userId)] = role;
        }

        public Task<TenantMembershipInfo?> FindMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        {
            if (_memberships.TryGetValue((tenantId, userId), out var role))
            {
                return Task.FromResult<TenantMembershipInfo?>(new TenantMembershipInfo(tenantId, userId, role, false));
            }

            return Task.FromResult<TenantMembershipInfo?>(null);
        }
    }

    private sealed class TestDbCommand : DbCommand
    {
        private string _commandText = string.Empty;

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText
        {
            get => _commandText;
            set => _commandText = value ?? string.Empty;
        }

        public override int CommandTimeout { get; set; }
        public override System.Data.CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override System.Data.UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => throw new NotImplementedException();
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => throw new NotImplementedException();
        protected override DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior) => throw new NotImplementedException();
    }
}
