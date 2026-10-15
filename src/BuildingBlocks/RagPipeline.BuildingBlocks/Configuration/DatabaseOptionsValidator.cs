namespace RagPipeline.BuildingBlocks.Configuration;

using System.Data.Common;
using Microsoft.Extensions.Options;

public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail($"Configuration key '{DatabaseOptions.ConfigurationKey}' is required and cannot be empty.");
        }

        DbConnectionStringBuilder builder;
        try
        {
            builder = new DbConnectionStringBuilder
            {
                ConnectionString = options.ConnectionString
            };
        }
        catch (Exception)
        {
            return ValidateOptionsResult.Fail($"Configuration key '{DatabaseOptions.ConfigurationKey}' is malformed and could not be parsed as a connection string.");
        }

        var missingKeys = new List<string>();
        if (!builder.ContainsKey("Host") || string.IsNullOrWhiteSpace(builder["Host"]?.ToString()))
        {
            missingKeys.Add("Host");
        }

        if (!builder.ContainsKey("Database") || string.IsNullOrWhiteSpace(builder["Database"]?.ToString()))
        {
            missingKeys.Add("Database");
        }

        if (missingKeys.Count > 0)
        {
            return ValidateOptionsResult.Fail(
                $"Configuration key '{DatabaseOptions.ConfigurationKey}' is missing required key(s): {string.Join(", ", missingKeys)}.");
        }

        return ValidateOptionsResult.Success;
    }
}
