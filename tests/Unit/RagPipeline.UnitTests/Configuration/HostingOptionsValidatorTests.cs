namespace RagPipeline.UnitTests.Configuration;

using RagPipeline.BuildingBlocks.Configuration;
using Xunit;

public class HostingOptionsValidatorTests
{
    private readonly HostingOptionsValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenApplicationNameMissingOrEmpty_Fails(string? appName)
    {
        var options = new HostingOptions { ApplicationName = appName! };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Hosting:ApplicationName", result.FailureMessage);
        Assert.Contains("required", result.FailureMessage);
    }

    [Fact]
    public void Validate_WhenApplicationNameProvided_Succeeds()
    {
        var options = new HostingOptions { ApplicationName = "RagPipeline.Api" };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }
}
