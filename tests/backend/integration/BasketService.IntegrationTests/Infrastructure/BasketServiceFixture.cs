using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.Redis;
using Xunit;

namespace BasketService.IntegrationTests.Infrastructure;

public sealed class BasketServiceFixture
    : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer =
        new RedisBuilder("redis:8-alpine")
            .Build();

    private BasketServiceFactory? _factory;

    public HttpClient Client
    {
        get;
        private set;
    } = null!;

    internal TestCatalogClient CatalogClient
    {
        get;
    } = new();

    internal BasketServiceFactory Factory =>
        _factory
        ?? throw new InvalidOperationException(
            "The Basket Service factory has not been initialized.");

    internal string RedisConnectionString =>
        _redisContainer.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _redisContainer.StartAsync();

        _factory = new BasketServiceFactory(
            _redisContainer.GetConnectionString(),
            CatalogClient);

        Client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        _factory?.Dispose();

        await _redisContainer.DisposeAsync();
    }
}
