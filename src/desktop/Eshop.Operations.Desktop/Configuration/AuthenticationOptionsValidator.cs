using Microsoft.Extensions.Options;

namespace Eshop.Operations.Desktop.Configuration;

public sealed class AuthenticationOptionsValidator
    : IValidateOptions<AuthenticationOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        AuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Uri.TryCreate(
                options.Authority,
                UriKind.Absolute,
                out Uri? authority)
            || (authority.Scheme != Uri.UriSchemeHttp
                && authority.Scheme != Uri.UriSchemeHttps))
        {
            return ValidateOptionsResult.Fail(
                "Authentication:Authority must be an absolute HTTP or HTTPS URI.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            return ValidateOptionsResult.Fail(
                "Authentication:ClientId must be configured.");
        }

        string[] scopes =
            options.Scope.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        if (!scopes.Contains(
                "openid",
                StringComparer.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "Authentication:Scope must contain openid.");
        }

        if (options.CallbackTimeoutSeconds is < 30 or > 600)
        {
            return ValidateOptionsResult.Fail(
                "Authentication:CallbackTimeoutSeconds must be between 30 and 600.");
        }

        return ValidateOptionsResult.Success;
    }
}
