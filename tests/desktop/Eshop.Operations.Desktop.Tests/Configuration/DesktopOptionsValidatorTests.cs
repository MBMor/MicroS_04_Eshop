using Eshop.Operations.Desktop.Configuration;
using Xunit;

namespace Eshop.Operations.Desktop.Tests.Configuration;

public sealed class DesktopOptionsValidatorTests
{
    private readonly DesktopOptionsValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void ValidateWhenEnvironmentNameIsMissingReturnsFailure(
        string environmentName)
    {
        var options = new DesktopOptions
        {
            EnvironmentName = environmentName
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Desktop:EnvironmentName must be configured.",
            result.Failures);
    }

    [Fact]
    public void ValidateWhenEnvironmentNameIsConfiguredReturnsSuccess()
    {
        var options = new DesktopOptions
        {
            EnvironmentName = "Local"
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }
}
