namespace RagPipeline.BuildingBlocks.Configuration;

public sealed class HostingOptions
{
    public const string SectionName = "Hosting";
    public const string ConfigurationKey = "Hosting:ApplicationName";

    public string ApplicationName { get; set; } = string.Empty;
    public string DeploymentMode { get; set; } = "SelfHosted";
    public bool IsSaaS => string.Equals(DeploymentMode, "SaaS", StringComparison.OrdinalIgnoreCase);
}
