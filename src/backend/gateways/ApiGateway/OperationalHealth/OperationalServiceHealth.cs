namespace ApiGateway.OperationalHealth;

public sealed record OperationalServiceHealth(
    string Service,
    string Status,
    long DurationMilliseconds,
    string? FailureKind,
    int? HttpStatusCode);
