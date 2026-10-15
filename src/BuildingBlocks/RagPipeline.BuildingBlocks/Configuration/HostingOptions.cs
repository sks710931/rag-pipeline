namespace RagPipeline.BuildingBlocks.Configuration;

public sealed class HostingOptions
{
    public const string SectionName = "Hosting";
    public const string ConfigurationKey = "Hosting:ApplicationName";

    public string ApplicationName { get; set; } = string.Empty;
}
