namespace RagPipeline.IntegrationTests;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RagPipeline.BuildingBlocks.Modules;
using Xunit;

public class HostAndModulesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HostAndModulesTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Host_BuildsSuccessfully_AndRegistersAllThreeModules()
    {
        using var client = _factory.CreateClient();
        Assert.NotNull(client);

        using var scope = _factory.Services.CreateScope();
        var modules = scope.ServiceProvider.GetRequiredService<IEnumerable<IModule>>().ToList();

        Assert.Equal(3, modules.Count);

        var names = modules.Select(m => m.Name).ToList();
        Assert.Contains("IdentityAccess", names);
        Assert.Contains("TenantManagement", names);
        Assert.Contains("DeploymentCapabilities", names);
    }
}
