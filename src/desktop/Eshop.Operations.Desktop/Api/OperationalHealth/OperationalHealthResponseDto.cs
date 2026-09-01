namespace Eshop.Operations.Desktop.Api.OperationalHealth;

public sealed record OperationalHealthResponseDto(
    string Status,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<OperationalServiceHealthDto> Services);
