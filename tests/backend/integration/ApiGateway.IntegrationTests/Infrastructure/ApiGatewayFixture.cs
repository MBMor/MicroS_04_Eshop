using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ApiGateway.IntegrationTests.Infrastructure;

public sealed class ApiGatewayFixture
    : IAsyncLifetime
{
    private FakeDownstreamServer?
        _downstreamServer;

    private ApiGatewayFactory?
        _factory;

    public HttpClient Client { get; private set; } =
        null!;

    public int ForwardedRequestCount =>
        (_downstreamServer
         ?? throw new InvalidOperationException(
             "The downstream server is not initialized."))
        .RequestCount;

    public void ResetForwardedRequestCount()
    {
        (_downstreamServer
         ?? throw new InvalidOperationException(
             "The downstream server is not initialized."))
        .ResetRequestCount();
    }

    public async ValueTask InitializeAsync()
    {
        _downstreamServer =
            await FakeDownstreamServer.StartAsync();

        _factory = new ApiGatewayFactory(
            _downstreamServer.BaseAddress);

        Client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        _factory?.Dispose();

        if (_downstreamServer is not null)
        {
            await _downstreamServer.DisposeAsync();
        }
    }
}
