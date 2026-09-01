namespace ApiGateway.OperationalHealth;

internal sealed record DownstreamHealthResponse(
    string Status,
    IReadOnlyList<DownstreamHealthCheck> Checks);

internal sealed record DownstreamHealthCheck(
    string Name,
    string Status);
