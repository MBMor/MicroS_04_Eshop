namespace ApiGateway.RateLimiting;

public sealed class GatewayRateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public FixedWindowRateLimitOptions PublicRead { get; init; } =
        new();

    public FixedWindowRateLimitOptions CustomerApi { get; init; } =
        new();

    public FixedWindowRateLimitOptions Checkout { get; init; } =
        new();

    public FixedWindowRateLimitOptions Operational { get; init; } =
        new();
}

public sealed class FixedWindowRateLimitOptions
{
    public int PermitLimit { get; init; }

    public int WindowSeconds { get; init; }
}
