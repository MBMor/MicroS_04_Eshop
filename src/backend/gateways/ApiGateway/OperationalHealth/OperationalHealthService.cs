using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

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
                0,
                "Configuration",
                null,
                []);
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

            bool isHealthy =
                response.IsSuccessStatusCode;

            IReadOnlyList<string>
                failedDependencies =
                    isHealthy
                        ? []
                        : await ReadFailedDependenciesAsync(
                            response,
                            timeout.Token);

            return new OperationalServiceHealth(
                target.Service,
                isHealthy
                    ? "Healthy"
                    : "Unhealthy",
                (long)Stopwatch
                    .GetElapsedTime(started)
                    .TotalMilliseconds,
                isHealthy
                    ? null
                    : "HttpStatus",
                (int)response.StatusCode,
                failedDependencies);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return new OperationalServiceHealth(
                target.Service,
                "Unavailable",
                (long)Stopwatch
                    .GetElapsedTime(started)
                    .TotalMilliseconds,
                "Timeout",
                null,
                []);
        }
        catch (HttpRequestException)
        {
            return new OperationalServiceHealth(
                target.Service,
                "Unavailable",
                (long)Stopwatch
                    .GetElapsedTime(started)
                    .TotalMilliseconds,
                "Connection",
                null,
                []);
        }
    }

    private static async Task<IReadOnlyList<string>>
        ReadFailedDependenciesAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
    {
        try
        {
            DownstreamHealthResponse? health =
                await response.Content
                    .ReadFromJsonAsync<DownstreamHealthResponse>(
                        cancellationToken: cancellationToken);

            if (health?.Checks is null)
            {
                return [];
            }

            return health.Checks
                .Where(
                    check =>
                        !string.Equals(
                            check.Status,
                            "Healthy",
                            StringComparison.OrdinalIgnoreCase))
                .Select(
                    check =>
                        check.Name)
                .Where(
                    name =>
                        !string.IsNullOrWhiteSpace(
                            name))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    name =>
                        name,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
        catch (NotSupportedException)
        {
            return [];
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
