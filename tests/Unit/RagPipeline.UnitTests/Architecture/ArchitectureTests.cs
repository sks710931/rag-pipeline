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
        var csprojFiles = Directory.GetFiles(repoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains("node_modules")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.NotEmpty(csprojFiles);

        foreach (var csprojFile in csprojFiles)
        {
            var fileName = Path.GetFileName(csprojFile);
            var isPersistenceOrTest = fileName.Contains("Persistence", StringComparison.OrdinalIgnoreCase)
                || fileName.Contains("Test", StringComparison.OrdinalIgnoreCase);

            var prohibitedTokens = isPersistenceOrTest
                ? new[] { "Qdrant", "OpenAI" }
                : new[] { "Qdrant", "OpenAI", "EntityFramework" };

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

    [Fact]
    public void ModuleCsprojFiles_OnlyHaveProjectReferenceToBuildingBlocks()
    {
        var repoRoot = FindRepoRoot();
        var srcDir = Path.Combine(repoRoot, "src");
        var csprojFiles = Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories);

        Assert.NotEmpty(csprojFiles);

        foreach (var csprojFile in csprojFiles)
        {
            var content = File.ReadAllText(csprojFile);
            AssertModuleProjectReferencesOnlyReferenceBuildingBlocks(content, Path.GetFileName(csprojFile));
        }
    }

    [Fact]
    public void Helper_FlagsModuleCsproj_ReferencingAnotherModule_EvenWithNoTypeUsage()
    {
        var inMemoryCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\..\BuildingBlocks\RagPipeline.BuildingBlocks\RagPipeline.BuildingBlocks.csproj" />
                <ProjectReference Include="..\..\IdentityAccess\RagPipeline.IdentityAccess\RagPipeline.IdentityAccess.csproj" />
              </ItemGroup>
            </Project>
            """;

        var disallowedReferences = FindDisallowedModuleProjectReferences(inMemoryCsproj);

        Assert.NotEmpty(disallowedReferences);
        Assert.Contains(disallowedReferences, r => r.Contains("RagPipeline.IdentityAccess", StringComparison.OrdinalIgnoreCase));

        Assert.ThrowsAny<Exception>(() =>
            AssertModuleProjectReferencesOnlyReferenceBuildingBlocks(inMemoryCsproj, "InMemorySampleWithCrossModuleReference"));
    }

    internal static IReadOnlyList<string> FindDisallowedModuleProjectReferences(string csprojXmlContent)
    {
        var doc = XDocument.Parse(csprojXmlContent);
        return FindDisallowedModuleProjectReferences(doc);
    }

    internal static IReadOnlyList<string> FindDisallowedModuleProjectReferences(XDocument doc)
    {
        var projectReferences = doc.Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();

        var disallowed = new List<string>();
        foreach (var reference in projectReferences)
        {
            var normalized = reference.Replace('\\', '/');
            var projectName = Path.GetFileNameWithoutExtension(normalized);

            var isBuildingBlocks = string.Equals(projectName, "RagPipeline.BuildingBlocks", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith("/RagPipeline.BuildingBlocks.csproj", StringComparison.OrdinalIgnoreCase);

            if (!isBuildingBlocks)
            {
                disallowed.Add(reference);
            }
        }

        return disallowed;
    }

    internal static void AssertModuleProjectReferencesOnlyReferenceBuildingBlocks(string csprojXmlContent, string? projectIdentifier = null)
    {
        var disallowed = FindDisallowedModuleProjectReferences(csprojXmlContent);
        Assert.True(
            disallowed.Count == 0,
            $"Module project '{projectIdentifier ?? "in-memory"}' contains disallowed ProjectReference(s): {string.Join(", ", disallowed)}. Modules may only reference BuildingBlocks.");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "RagPipeline.slnx")))
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

        throw new InvalidOperationException("Could not locate repository root containing RagPipeline.slnx.");
    }
}
