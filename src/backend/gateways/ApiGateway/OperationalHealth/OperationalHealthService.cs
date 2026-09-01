using System.Diagnostics;

namespace ApiGateway.OperationalHealth;

public sealed class OperationalHealthService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    private static readonly ServiceTarget[] Targets =
    [
        new("Catalog", "catalog-cluster"),
        new("Basket", "basket-cluster"),
        new("Orders", "orders-cluster"),
        new("Inventory", "inventory-cluster"),
        new("Payments", "payments-cluster"),
        new("Notifications", "notifications-cluster")
    ];

    public async Task<OperationalHealthResponse> CheckAsync(
        CancellationToken cancellationToken)
    {
        Task<OperationalServiceHealth>[] checks =
            Targets
                .Select(
                    target =>
                        CheckServiceAsync(
                            target,
                            cancellationToken))
                .ToArray();

        OperationalServiceHealth[] services =
            await Task.WhenAll(checks);

        string overallStatus =
            services.All(
                service =>
                    string.Equals(
                        service.Status,
                        "Healthy",
                        StringComparison.Ordinal))
                ? "Healthy"
                : "Degraded";

        return new OperationalHealthResponse(
            overallStatus,
            DateTimeOffset.UtcNow,
            services);
    }

    private async Task<OperationalServiceHealth>
        CheckServiceAsync(
            ServiceTarget target,
            CancellationToken cancellationToken)
    {
        string? address =
            configuration[
                $"ReverseProxy:Clusters:{target.ClusterId}:Destinations:{GetDestinationName(target.ClusterId)}:Address"];

        if (string.IsNullOrWhiteSpace(address)
            || !Uri.TryCreate(
                address,
                UriKind.Absolute,
                out Uri? baseUri))
        {
            return new OperationalServiceHealth(
                target.Service,
                "Unknown",
                0);
        }

        Uri healthUri =
            new(
                baseUri,
                "health");

        using HttpClient httpClient =
            httpClientFactory.CreateClient(
                "OperationalHealthProbe");

        using CancellationTokenSource timeout =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        timeout.CancelAfter(
            TimeSpan.FromSeconds(2));

        long started =
            Stopwatch.GetTimestamp();

        try
        {
            using HttpResponseMessage response =
                await httpClient.GetAsync(
                    healthUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);

            return new OperationalServiceHealth(
                target.Service,
                response.IsSuccessStatusCode
                    ? "Healthy"
                    : "Unhealthy",
                (long)Stopwatch
                    .GetElapsedTime(started)
                    .TotalMilliseconds);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return new OperationalServiceHealth(
                target.Service,
                "Unavailable",
                (long)Stopwatch
                    .GetElapsedTime(started)
                    .TotalMilliseconds);
        }
        catch (HttpRequestException)
        {
            return new OperationalServiceHealth(
                target.Service,
                "Unavailable",
                (long)Stopwatch
                    .GetElapsedTime(started)
                    .TotalMilliseconds);
        }
    }

    private static string GetDestinationName(
        string clusterId)
    {
        return clusterId.Replace(
            "-cluster",
            "-service",
            StringComparison.Ordinal);
    }

    private sealed record ServiceTarget(
        string Service,
        string ClusterId);
}
