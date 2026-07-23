using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiGateway.IntegrationTests.Infrastructure;

internal sealed class GatewayRateLimitingTestHost
    : IAsyncDisposable
{
    private readonly FakeDownstreamServer
        _downstreamServer;

    private readonly ApiGatewayFactory _factory;

    private GatewayRateLimitingTestHost(
        FakeDownstreamServer downstreamServer,
        ApiGatewayFactory factory,
        HttpClient client)
    {
        _downstreamServer = downstreamServer;
        _factory = factory;
        Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<GatewayRateLimitingTestHost>
        StartAsync()
    {
        FakeDownstreamServer downstreamServer =
            await FakeDownstreamServer.StartAsync();

        Dictionary<string, string?> overrides = new(
            StringComparer.OrdinalIgnoreCase)
        {
            ["RateLimiting:PublicRead:PermitLimit"] =
                "2",

            ["RateLimiting:PublicRead:WindowSeconds"] =
                "60",

            ["RateLimiting:CustomerApi:PermitLimit"] =
                "2",

            ["RateLimiting:CustomerApi:WindowSeconds"] =
                "60",

            ["RateLimiting:Checkout:PermitLimit"] =
                "1",

            ["RateLimiting:Checkout:WindowSeconds"] =
                "60",

            ["RateLimiting:Operational:PermitLimit"] =
                "1",

            ["RateLimiting:Operational:WindowSeconds"] =
                "60"
        };

        ApiGatewayFactory factory = new(
            downstreamServer.BaseAddress,
            overrides);

        HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        return new GatewayRateLimitingTestHost(
            downstreamServer,
            factory,
            client);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        _factory.Dispose();

        await _downstreamServer.DisposeAsync();
    }
}
