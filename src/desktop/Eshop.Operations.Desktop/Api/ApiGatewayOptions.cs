namespace Eshop.Operations.Desktop.Api;

public sealed class ApiGatewayOptions
{
    public const string SectionName = "ApiGateway";

    public string BaseAddress { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 15;
}
