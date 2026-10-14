namespace RagPipeline.BuildingBlocks.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";
    public const string ConfigurationKey = "ConnectionStrings:Database";

    public string? ConnectionString { get; set; }
}
