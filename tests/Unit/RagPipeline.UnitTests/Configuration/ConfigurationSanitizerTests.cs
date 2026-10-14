namespace RagPipeline.UnitTests.Configuration;

using Microsoft.Extensions.Options;
using RagPipeline.BuildingBlocks.Configuration;
using Xunit;

public class ConfigurationSanitizerTests
{
    [Fact]
    public void Sanitize_WithOptionsValidationException_IncludesOptionNameAndSanitizedFailures()
    {
        var failures = new List<string>
        {
            "Configuration key 'ConnectionStrings:Database' is missing required key(s): Host, Database."
        };
        var exception = new OptionsValidationException("DatabaseOptions", typeof(DatabaseOptions), failures);

        var result = ConfigurationSanitizer.Sanitize(exception);

        Assert.Contains("DatabaseOptions", result);
        Assert.Contains("ConnectionStrings:Database", result);
        Assert.Contains("Host", result);
        Assert.Contains("Database", result);
    }

    [Fact]
    public void Sanitize_MasksPasswordsAndSecretsInFailureMessage()
    {
        var rawMessage = "Failed connection to Password=SECRETVALUE123;garbage and secret=MY_TOP_SECRET_TOKEN";

        var sanitized = ConfigurationSanitizer.SanitizeMessage(rawMessage);

        Assert.Contains("Password=***", sanitized);
        Assert.Contains("secret=***", sanitized);
        Assert.DoesNotContain("SECRETVALUE123", sanitized);
        Assert.DoesNotContain("MY_TOP_SECRET_TOKEN", sanitized);
    }

    [Fact]
    public void Sanitize_StripsStackTraceFromDiagnosticMessage()
    {
        var messageWithStack = "Configuration key 'ConnectionStrings:Database' failed\n   at System.Data.Common.DbConnectionStringBuilder.set_ConnectionString(String value)\n   at RagPipeline.BuildingBlocks.Configuration.DatabaseOptionsValidator.Validate";

        var sanitized = ConfigurationSanitizer.SanitizeMessage(messageWithStack);

        Assert.Contains("ConnectionStrings:Database", sanitized);
        Assert.DoesNotContain("DbConnectionStringBuilder", sanitized);
        Assert.DoesNotContain("   at ", sanitized);
    }

    [Fact]
    public void SanitizeMessage_WhenNullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ConfigurationSanitizer.SanitizeMessage(null));
        Assert.Equal(string.Empty, ConfigurationSanitizer.SanitizeMessage("   "));
    }
}
