namespace RagPipeline.UnitTests.Configuration;

using RagPipeline.BuildingBlocks.Configuration;
using Xunit;

public class DatabaseOptionsValidatorTests
{
    private readonly DatabaseOptionsValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenConnectionStringMissingOrEmpty_Fails(string? connectionString)
    {
        var options = new DatabaseOptions { ConnectionString = connectionString };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ConnectionStrings:Database", result.FailureMessage);
        Assert.Contains("required", result.FailureMessage);
    }

    [Fact]
    public void Validate_WhenConnectionStringIsMalformed_FailsWithoutExposingDetails()
    {
        var options = new DatabaseOptions
        {
            ConnectionString = "Password=SECRETVALUE123;garbage"
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ConnectionStrings:Database", result.FailureMessage);
        Assert.Contains("malformed", result.FailureMessage);
        Assert.DoesNotContain("SECRETVALUE123", result.FailureMessage);
        Assert.DoesNotContain("garbage", result.FailureMessage);
    }

    [Fact]
    public void Validate_WhenMissingHostAndDatabase_FailsReportingMissingKeysWithoutSecrets()
    {
        var options = new DatabaseOptions
        {
            ConnectionString = "Password=SECRETVALUE123;Port=5432"
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ConnectionStrings:Database", result.FailureMessage);
        Assert.Contains("Host", result.FailureMessage);
        Assert.Contains("Database", result.FailureMessage);
        Assert.DoesNotContain("SECRETVALUE123", result.FailureMessage);
    }

    [Fact]
    public void Validate_WhenMissingOnlyDatabase_FailsReportingDatabase()
    {
        var options = new DatabaseOptions
        {
            ConnectionString = "Host=localhost;Username=ragpipeline"
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ConnectionStrings:Database", result.FailureMessage);
        Assert.Contains("Database", result.FailureMessage);
        Assert.DoesNotContain("Host", result.FailureMessage);
    }

    [Fact]
    public void Validate_WhenValidConnectionString_Succeeds()
    {
        var options = new DatabaseOptions
        {
            ConnectionString = "Host=localhost;Database=ragpipeline;Username=ragpipeline"
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }
}
