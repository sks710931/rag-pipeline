namespace RagPipeline.UnitTests.Readiness;

using Microsoft.Extensions.Configuration;
using RagPipeline.BuildingBlocks.Readiness;
using Xunit;

public class ReadinessAggregatorTests
{
    [Fact]
    public async Task AggregateAsync_WhenAllChecksReady_ReturnsReadyStatus()
    {
        var checks = new IReadinessCheck[]
        {
            new TestCheck("database", ReadinessStatus.Ready)
        };

        var report = await ReadinessAggregator.AggregateAsync(checks, configuration: null);

        Assert.Equal("ready", report.Status);
        Assert.True(report.IsReady);
        Assert.Equal("ready", report.Dependencies["database"]);
    }

    [Fact]
    public async Task AggregateAsync_WhenDatabaseUnchecked_ReturnsNotReadyStatus()
    {
        var checks = new IReadinessCheck[]
        {
            new DatabaseReadinessCheck()
        };

        var report = await ReadinessAggregator.AggregateAsync(checks, configuration: null);

        Assert.Equal("not_ready", report.Status);
        Assert.False(report.IsReady);
        Assert.Equal("unchecked", report.Dependencies["database"]);
    }

    [Fact]
    public async Task AggregateAsync_WhenCheckNotReady_ReturnsNotReadyStatus()
    {
        var checks = new IReadinessCheck[]
        {
            new TestCheck("database", ReadinessStatus.NotReady)
        };

        var report = await ReadinessAggregator.AggregateAsync(checks, configuration: null);

        Assert.Equal("not_ready", report.Status);
        Assert.False(report.IsReady);
        Assert.Equal("not_ready", report.Dependencies["database"]);
    }

    [Fact]
    public async Task AggregateAsync_WhenCheckNotConfigured_ReturnsNotReadyStatus()
    {
        var checks = new IReadinessCheck[]
        {
            new TestCheck("database", ReadinessStatus.NotConfigured)
        };

        var report = await ReadinessAggregator.AggregateAsync(checks, configuration: null);

        Assert.Equal("not_ready", report.Status);
        Assert.False(report.IsReady);
        Assert.Equal("not_configured", report.Dependencies["database"]);
    }

    [Fact]
    public async Task AggregateAsync_WhenOptionalDependenciesAbsent_ReportsNotConfigured_AndDoesNotBlockReadiness()
    {
        var checks = new IReadinessCheck[]
        {
            new TestCheck("database", ReadinessStatus.Ready)
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var report = await ReadinessAggregator.AggregateAsync(checks, config);

        Assert.Equal("ready", report.Status);
        Assert.True(report.IsReady);
        Assert.Equal("ready", report.Dependencies["database"]);
        Assert.Equal("not_configured", report.Dependencies["qdrant"]);
        Assert.Equal("not_configured", report.Dependencies["ai"]);
        Assert.Equal("not_configured", report.Dependencies["azure"]);
    }

    [Fact]
    public async Task AggregateAsync_WhenOptionalDependenciesConfigured_ReportsConfigured_AndNeverEchoesConfigValues()
    {
        const string fakeQdrantKey = "qdrant-secret-key-123";
        const string fakeAiKey = "sk-fake-ai-secret-456";
        const string fakeAzureKey = "azure-ocr-key-789";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Qdrant:Endpoint"] = "http://localhost:6333",
                ["Qdrant:ApiKey"] = fakeQdrantKey,
                ["Ai:ApiKey"] = fakeAiKey,
                ["Azure:ApiKey"] = fakeAzureKey
            })
            .Build();

        var checks = new IReadinessCheck[]
        {
            new TestCheck("database", ReadinessStatus.Ready)
        };

        var report = await ReadinessAggregator.AggregateAsync(checks, config);

        Assert.Equal("ready", report.Status);
        Assert.True(report.IsReady);
        Assert.Equal("ready", report.Dependencies["database"]);
        Assert.Equal("configured", report.Dependencies["qdrant"]);
        Assert.Equal("configured", report.Dependencies["ai"]);
        Assert.Equal("configured", report.Dependencies["azure"]);

        // Verify secrets are strictly not echoed in dictionary values
        foreach (var value in report.Dependencies.Values)
        {
            Assert.DoesNotContain(fakeQdrantKey, value);
            Assert.DoesNotContain(fakeAiKey, value);
            Assert.DoesNotContain(fakeAzureKey, value);
        }
    }

    [Fact]
    public async Task AggregateAsync_WhenNoDatabaseCheckProvided_DefaultsToNotConfigured_AndNotReady()
    {
        var checks = Array.Empty<IReadinessCheck>();

        var report = await ReadinessAggregator.AggregateAsync(checks, configuration: null);

        Assert.Equal("not_ready", report.Status);
        Assert.False(report.IsReady);
        Assert.Equal("not_configured", report.Dependencies["database"]);
    }

    [Fact]
    public async Task AggregateAsync_WhenMultipleChecksWithSameName_TakesLastRegistered()
    {
        var checks = new IReadinessCheck[]
        {
            new TestCheck("database", ReadinessStatus.Unchecked),
            new TestCheck("database", ReadinessStatus.Ready)
        };

        var report = await ReadinessAggregator.AggregateAsync(checks, configuration: null);

        Assert.Equal("ready", report.Status);
        Assert.True(report.IsReady);
        Assert.Equal("ready", report.Dependencies["database"]);
    }

    [Fact]
    public async Task AggregateAsync_WhenCheckThrowsException_ReportsNotReadyGracefully()
    {
        var checks = new IReadinessCheck[]
        {
            new FaultyCheck("database")
        };

        var report = await ReadinessAggregator.AggregateAsync(checks, configuration: null);

        Assert.Equal("not_ready", report.Status);
        Assert.False(report.IsReady);
        Assert.Equal("not_ready", report.Dependencies["database"]);
    }

    [Fact]
    public async Task AggregateAsync_WhenCancellationRequested_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var checks = new IReadinessCheck[]
        {
            new TestCheck("database", ReadinessStatus.Ready)
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ReadinessAggregator.AggregateAsync(checks, configuration: null, cts.Token));
    }

    [Fact]
    public void Throwaway_DeliberateFailure_ProvesBuildTestGateFails()
    {
        Assert.Equal(1, 2);
    }

    private sealed class TestCheck(string name, ReadinessStatus status) : IReadinessCheck
    {
        public string Name => name;

        public Task<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ReadinessResult(status));
        }
    }

    private sealed class FaultyCheck(string name) : IReadinessCheck
    {
        public string Name => name;

        public Task<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated connection failure.");
        }
    }
}
