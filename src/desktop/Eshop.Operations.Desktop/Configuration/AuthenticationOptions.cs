namespace Eshop.Operations.Desktop.Configuration;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string Authority { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public int CallbackTimeoutSeconds { get; init; } = 180;
}
