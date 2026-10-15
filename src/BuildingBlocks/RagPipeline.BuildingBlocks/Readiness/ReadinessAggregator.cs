namespace RagPipeline.BuildingBlocks.Readiness;

using Microsoft.Extensions.Configuration;

public static class ReadinessAggregator
{
    public static async Task<ReadinessReport> AggregateAsync(
        IEnumerable<IReadinessCheck>? checks,
        IConfiguration? configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveChecks = (checks ?? Enumerable.Empty<IReadinessCheck>())
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();

        var dependencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allReady = effectiveChecks.Count > 0;

        foreach (var check in effectiveChecks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReadinessResult result;
            try
            {
                result = await check.CheckAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                result = ReadinessResult.NotReady("Check execution failed.");
            }

            if (result.Status != ReadinessStatus.Ready)
            {
                allReady = false;
            }

            dependencies[check.Name.ToLowerInvariant()] = result.Status.ToReportString();
        }

        if (!dependencies.ContainsKey(DatabaseReadinessCheck.DefaultName))
        {
            dependencies[DatabaseReadinessCheck.DefaultName] = ReadinessStatus.NotConfigured.ToReportString();
            allReady = false;
        }

        // Optional dependencies from configuration presence (never block readiness)
        dependencies["qdrant"] = IsDependencyConfigured(configuration, "Qdrant", "Qdrant:Endpoint", "Qdrant:Url", "ConnectionStrings:Qdrant")
            ? "configured"
            : "not_configured";

        dependencies["ai"] = IsDependencyConfigured(configuration, "Ai", "Ai:ApiKey", "Ai:Endpoint", "OpenAi:ApiKey")
            ? "configured"
            : "not_configured";

        dependencies["azure"] = IsDependencyConfigured(configuration, "Azure", "Azure:ApiKey", "Azure:Endpoint", "Azure:OcrEndpoint")
            ? "configured"
            : "not_configured";

        var overallStatus = allReady ? "ready" : "not_ready";
        return new ReadinessReport(overallStatus, dependencies);
    }

    public static bool IsDependencyConfigured(IConfiguration? configuration, string sectionName, params string[] explicitKeys)
    {
        if (configuration == null)
        {
            return false;
        }

        foreach (var key in explicitKeys)
        {
            if (!string.IsNullOrWhiteSpace(configuration[key]))
            {
                return true;
            }
        }

        var section = configuration.GetSection(sectionName);
        if (section.Exists() && section.GetChildren().Any(c => !string.IsNullOrWhiteSpace(c.Value)))
        {
            return true;
        }

        return false;
    }
}
