using Microsoft.Extensions.Options;

namespace Eshop.Operations.Desktop.Configuration;

public sealed class DesktopOptionsValidator : IValidateOptions<DesktopOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        DesktopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.EnvironmentName))
        {
            return ValidateOptionsResult.Fail(
                "Desktop:EnvironmentName must be configured.");
        }

        return ValidateOptionsResult.Success;
    }
}
