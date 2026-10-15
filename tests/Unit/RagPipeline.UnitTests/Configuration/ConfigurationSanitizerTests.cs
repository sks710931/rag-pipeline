namespace RagPipeline.UnitTests.Configuration;

using Microsoft.Extensions.Options;
using RagPipeline.BuildingBlocks.Configuration;
using Xunit;

public class ConfigurationSanitizerTests
{
    private const string CanaryPassword = "CANARY-super-secret-password-xyz";
    private const string CanaryToken = "CANARY-api-token-987654";

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
    public void Sanitize_CanaryConnectionStringPasswordAndToken_NeverAppearsInSanitizeOutput()
    {
        var rawMessage = $"Configuration failed for Host=db.internal;Password={CanaryPassword};Token={CanaryToken};Timeout=30";

        var sanitizedMessage = ConfigurationSanitizer.SanitizeMessage(rawMessage);

        Assert.DoesNotContain(CanaryPassword, sanitizedMessage);
        Assert.DoesNotContain(CanaryToken, sanitizedMessage);
        Assert.Contains("Password=***", sanitizedMessage);
        Assert.Contains("Token=***", sanitizedMessage);

        var exception = new OptionsValidationException("DatabaseOptions", typeof(DatabaseOptions), new[] { rawMessage });
        var sanitizedException = ConfigurationSanitizer.Sanitize(exception);

        Assert.DoesNotContain(CanaryPassword, sanitizedException);
        Assert.DoesNotContain(CanaryToken, sanitizedException);
        Assert.Contains("Password=***", sanitizedException);
        Assert.Contains("Token=***", sanitizedException);
    }

    [Fact]
    public void Sanitize_WithOptionsValidationException_MultipleOptionTypesHandledGracefully()
    {
        var failures = new List<string>
        {
            "ReverseProxy:KnownProxies:0 is not a valid IP address",
            "Configuration key 'ConnectionStrings:Database' is missing required key(s): Host"
        };
        // Simulated multiple option failure where OptionsType is object and name is empty (standard in ASP.NET Core StartupValidator)
        var exception = new OptionsValidationException(string.Empty, typeof(object), failures);

        var result = ConfigurationSanitizer.Sanitize(exception);

        Assert.DoesNotContain("Object", result);
        Assert.Contains("Configuration validation failed:", result);
        Assert.Contains("ReverseProxy:KnownProxies:0", result);
        Assert.Contains("ConnectionStrings:Database", result);
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
