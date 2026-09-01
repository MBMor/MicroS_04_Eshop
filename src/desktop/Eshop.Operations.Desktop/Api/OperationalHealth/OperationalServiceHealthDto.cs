namespace Eshop.Operations.Desktop.Api.OperationalHealth;

public sealed record OperationalServiceHealthDto(
    string Service,
    string Status,
    long DurationMilliseconds,
    string? FailureKind,
    int? HttpStatusCode);
