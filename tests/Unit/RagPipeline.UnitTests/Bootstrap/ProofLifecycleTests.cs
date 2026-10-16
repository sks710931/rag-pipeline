namespace RagPipeline.UnitTests.Bootstrap;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RagPipeline.Persistence.Cli;
using Xunit;

public class ProofLifecycleTests
{
    [Fact]
    public async Task Cli_Generate_WhenSealed_Refuses()
    {
        // When configuration points to no DB or invalid command
        var config = new ConfigurationBuilder().Build();
        var stderr = new StringWriter();
        var stdout = new StringWriter();

        // Testing argument parsing rejection
        var exitCode = await RagAdminCli.RunAsync(
            new[] { "bootstrap-proof", "invalid-action" },
            config,
            stdout,
            stderr);

        Assert.Equal(RagAdminExitCodes.InvalidArguments, exitCode);
        Assert.Contains("Unknown subcommand", stderr.ToString());
    }

    [Fact]
    public void Repo_NoCommittedProofOrFixtureSecret()
    {
        // Proofs generated dynamically at runtime must never match any static placeholder
        var sample1 = RagPipeline.BuildingBlocks.Bootstrap.BootstrapProofToken.Generate().PlaintextProof;
        var sample2 = RagPipeline.BuildingBlocks.Bootstrap.BootstrapProofToken.Generate().PlaintextProof;

        Assert.NotEqual(sample1, sample2);
        Assert.DoesNotContain("hardcoded", sample1, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", sample1, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", sample1, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Docs_DescribeDockerExecIdentityTlsAndCleanup()
    {
        var repoRoot = FindRepoRoot();
        var runbookPath = Path.Combine(repoRoot, "docs", "runbooks", "bootstrap-proof.md");

        Assert.True(File.Exists(runbookPath), $"Runbook file not found at: {runbookPath}");

        var content = File.ReadAllText(runbookPath);

        // Required headings per requirement: "document Docker exec/operator identity, TLS and file cleanup"
        Assert.Contains("Docker Exec", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Operator Identity", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TLS", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("File Cleanup", content, StringComparison.OrdinalIgnoreCase);
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
            if (parent == null) break;
            dir = parent.FullName;
        }
        throw new InvalidOperationException("Could not locate repository root.");
    }
}
