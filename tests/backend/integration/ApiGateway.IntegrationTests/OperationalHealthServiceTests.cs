using System.Net;
using ApiGateway.OperationalHealth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Xunit;

namespace ApiGateway.IntegrationTests;

public sealed class OperationalHealthServiceTests
{
    [Fact]
    public async Task AllServicesHealthyProducesHealthyAggregate()
    {
        OperationalHealthService service =
            CreateService(
                new ProbeHandler(
                    (_, _) =>
                        Task.FromResult(
                            new HttpResponseMessage(
                                HttpStatusCode.OK))));

        OperationalHealthResponse response =
            await service.CheckAsync(
                TestContext.Current.CancellationToken);

        Assert.Equal(
            "Healthy",
            response.Status);
        Assert.Equal(
            6,
            response.Services.Count);
        Assert.All(
            response.Services,
            item => Assert.Equal("Healthy", item.Status));
    }

    [Fact]
    public async Task FailedServiceIsUnhealthyAndDegradesAggregate()
    {
        OperationalHealthService service =
            CreateService(
                new ProbeHandler(
                    (uri, _) =>
                        Task.FromResult(
                            new HttpResponseMessage(
                                uri.Host.StartsWith(
                                    "payments-service.",
                                    StringComparison.Ordinal)
                                    ? HttpStatusCode.InternalServerError
                                    : HttpStatusCode.OK))));

        OperationalHealthResponse response =
            await service.CheckAsync(
                TestContext.Current.CancellationToken);

        Assert.Equal(
            "Degraded",
            response.Status);
        Assert.Equal(
            "Unhealthy",
            Assert.Single(
                response.Services,
                item => item.Service == "Payments").Status);
    }

    [Fact]
    public async Task UnreachableServiceDoesNotFailAggregateRequest()
    {
        OperationalHealthService service =
            CreateService(
                new ProbeHandler(
                    (uri, _) =>
                    {
                        if (uri.Host.StartsWith(
                                "payments-service.",
                                StringComparison.Ordinal))
                        {
                            throw new HttpRequestException(
                                "Payments is unavailable.");
                        }

                        return Task.FromResult(
                            new HttpResponseMessage(
                                HttpStatusCode.OK));
                    }));

        OperationalHealthResponse response =
            await service.CheckAsync(
                TestContext.Current.CancellationToken);

        Assert.Equal(
            "Degraded",
            response.Status);
        Assert.Equal(
            "Unavailable",
            Assert.Single(
                response.Services,
                item => item.Service == "Payments").Status);
    }

    [Fact]
    public async Task SlowServiceIsBoundedByProbeTimeout()
    {
        OperationalHealthService service =
            CreateService(
                new ProbeHandler(
                    async (uri, cancellationToken) =>
                    {
                        if (uri.Host.StartsWith(
                                "payments-service.",
                                StringComparison.Ordinal))
                        {
                            await Task.Delay(
                                Timeout.InfiniteTimeSpan,
                                cancellationToken);
                        }

                        return new HttpResponseMessage(
                            HttpStatusCode.OK);
                    }));

        OperationalHealthResponse response =
            await service.CheckAsync(
                TestContext.Current.CancellationToken);

        Assert.Equal(
            "Unavailable",
            Assert.Single(
                response.Services,
                item => item.Service == "Payments").Status);
        Assert.Equal(
            "Degraded",
            response.Status);
    }

    [Fact]
    public async Task ProbesRunConcurrently()
    {
        ConcurrencyProbeHandler handler =
            new();
        OperationalHealthService service =
            CreateService(handler);

        OperationalHealthResponse response =
            await service.CheckAsync(
                TestContext.Current.CancellationToken);

        Assert.Equal(
            "Healthy",
            response.Status);
        Assert.True(
            handler.MaximumConcurrency > 1,
            $"Expected concurrent probes, observed {handler.MaximumConcurrency}.");
    }

    private static OperationalHealthService CreateService(
        HttpMessageHandler handler)
    {
        return new OperationalHealthService(
            new TestHttpClientFactory(handler),
            CreateConfiguration());
    }

    private static IConfiguration CreateConfiguration()
    {
        (string Cluster, string Destination)[] targets =
        [
            ("catalog-cluster", "catalog-service"),
            ("basket-cluster", "basket-service"),
            ("orders-cluster", "orders-service"),
            ("inventory-cluster", "inventory-service"),
            ("payments-cluster", "payments-service"),
            ("notifications-cluster", "notifications-service")
        ];

        Dictionary<string, string?> values =
            new(StringComparer.OrdinalIgnoreCase);

        foreach ((string cluster, string destination) in targets)
        {
            values[
                $"ReverseProxy:Clusters:{cluster}:Destinations:{destination}:Address"] =
                $"http://{destination}.test/";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TestHttpClientFactory(
        HttpMessageHandler handler)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(
            string name)
        {
            return new HttpClient(
                handler,
                disposeHandler: false);
        }
    }

    private sealed class ProbeHandler(
        Func<Uri, CancellationToken, Task<HttpResponseMessage>> probe)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return probe(
                request.RequestUri
                ?? throw new InvalidOperationException(
                    "Probe request URI is missing."),
                cancellationToken);
        }
    }

    private sealed class ConcurrencyProbeHandler
        : HttpMessageHandler
    {
        private int _active;
        private int _maximumConcurrency;

        public int MaximumConcurrency =>
            Volatile.Read(ref _maximumConcurrency);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            int active =
                Interlocked.Increment(ref _active);

            int observedMaximum;
            do
            {
                observedMaximum =
                    Volatile.Read(ref _maximumConcurrency);
            }
            while (active > observedMaximum
                   && Interlocked.CompareExchange(
                       ref _maximumConcurrency,
                       active,
                       observedMaximum) != observedMaximum);

            try
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(100),
                    cancellationToken);

                return new HttpResponseMessage(
                    HttpStatusCode.OK);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }
}
