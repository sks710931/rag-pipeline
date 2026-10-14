namespace RagPipeline.BuildingBlocks.Configuration;

using Microsoft.Extensions.Options;

public sealed class HostingOptionsValidator : IValidateOptions<HostingOptions>
{
    public ValidateOptionsResult Validate(string? name, HostingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApplicationName))
        {
            return ValidateOptionsResult.Fail($"Configuration key '{HostingOptions.ConfigurationKey}' is required and cannot be empty.");
        }

        return ValidateOptionsResult.Success;
    }
}
