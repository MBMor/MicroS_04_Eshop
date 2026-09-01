namespace Eshop.HealthChecks;

public sealed record EshopHealthResponse(
    string Status,
    IReadOnlyList<EshopHealthCheckResponse> Checks);

public sealed record EshopHealthCheckResponse(
    string Name,
    string Status);
