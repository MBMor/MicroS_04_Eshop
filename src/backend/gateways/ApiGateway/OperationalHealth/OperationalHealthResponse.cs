namespace ApiGateway.OperationalHealth;

public sealed record OperationalHealthResponse(
    string Status,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<OperationalServiceHealth> Services);
