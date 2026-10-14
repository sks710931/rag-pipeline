namespace RagPipeline.UnitTests.Architecture;

using System.Reflection;
using System.Xml.Linq;
using RagPipeline.BuildingBlocks.Modules;
using RagPipeline.DeploymentCapabilities;
using RagPipeline.IdentityAccess;
using RagPipeline.TenantManagement;
using Xunit;

public class ArchitectureTests
{
    private static readonly Assembly BuildingBlocksAssembly = typeof(IModule).Assembly;
    private static readonly Assembly IdentityAccessAssembly = typeof(IdentityAccessModule).Assembly;
    private static readonly Assembly TenantManagementAssembly = typeof(TenantManagementModule).Assembly;
    private static readonly Assembly DeploymentCapabilitiesAssembly = typeof(DeploymentCapabilitiesModule).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void ModuleAssemblies_ReferenceOnlyBuildingBlocks_AndNeverEachOther()
    {
        var modules = new Dictionary<string, Assembly>
        {
            ["IdentityAccess"] = IdentityAccessAssembly,
            ["TenantManagement"] = TenantManagementAssembly,
            ["DeploymentCapabilities"] = DeploymentCapabilitiesAssembly
        };

        foreach (var (currentModuleName, currentAssembly) in modules)
        {
            var referencedAssemblyNames = currentAssembly
                .GetReferencedAssemblies()
                .Select(a => a.Name ?? string.Empty)
                .ToList();

            // Module must reference BuildingBlocks
            Assert.Contains(BuildingBlocksAssembly.GetName().Name!, referencedAssemblyNames);

            // Module must never reference RagPipeline.Api
            Assert.DoesNotContain(ApiAssembly.GetName().Name!, referencedAssemblyNames);

            // Module must never reference any other module
            foreach (var (otherModuleName, otherAssembly) in modules)
            {
                if (currentModuleName == otherModuleName)
                {
                    continue;
                }

                Assert.DoesNotContain(otherAssembly.GetName().Name!, referencedAssemblyNames);
            }
        }
    }

    [Fact]
    public void NoProject_ReferencesProhibitedAssemblies()
    {
        var allAssemblies = new[]
        {
            BuildingBlocksAssembly,
            IdentityAccessAssembly,
            TenantManagementAssembly,
            DeploymentCapabilitiesAssembly,
            ApiAssembly
        };

        var prohibitedTokens = new[] { "Qdrant", "OpenAI", "EntityFramework" };

        foreach (var assembly in allAssemblies)
        {
            var referencedNames = assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name ?? string.Empty);

            foreach (var referencedName in referencedNames)
            {
                foreach (var token in prohibitedTokens)
                {
                    Assert.DoesNotContain(token, referencedName, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    [Fact]
    public void NoCsproj_ReferencesProhibitedPackagesOrProjects()
    {
        var repoRoot = FindRepoRoot();
        var csprojFiles = Directory.GetFiles(repoRoot, "*.csproj", SearchOption.AllDirectories);
        Assert.NotEmpty(csprojFiles);

        var prohibitedTokens = new[] { "Qdrant", "OpenAI", "EntityFramework" };

        foreach (var csprojFile in csprojFiles)
        {
            var doc = XDocument.Load(csprojFile);

            var packageRefs = doc.Descendants("PackageReference")
                .Select(e => (string?)e.Attribute("Include") ?? string.Empty);

            var projectRefs = doc.Descendants("ProjectReference")
                .Select(e => (string?)e.Attribute("Include") ?? string.Empty);

            foreach (var pkg in packageRefs)
            {
                foreach (var token in prohibitedTokens)
                {
                    Assert.DoesNotContain(token, pkg, StringComparison.OrdinalIgnoreCase);
                }
            }

            foreach (var proj in projectRefs)
            {
                foreach (var token in prohibitedTokens)
                {
                    Assert.DoesNotContain(token, proj, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "RagPipeline.slnx")) || File.Exists(Path.Combine(dir, "global.json")))
            {
                return dir;
            }

            var parent = Directory.GetParent(dir);
            if (parent == null)
            {
                break;
            }

            dir = parent.FullName;
        }

        return Directory.GetCurrentDirectory();
    }
}
