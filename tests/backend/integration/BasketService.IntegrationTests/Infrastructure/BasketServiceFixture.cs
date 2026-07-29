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
    private string? _redisConnectionString;

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
        _redisConnectionString
        ?? throw new InvalidOperationException(
            "The Redis connection string has not been initialized.");

    public Task PauseRedisAsync(
        CancellationToken cancellationToken = default)
    {
        return _redisContainer.PauseAsync(
            cancellationToken);
    }

    public Task UnpauseRedisAsync(
        CancellationToken cancellationToken = default)
    {
        return _redisContainer.UnpauseAsync(
            cancellationToken);
    }

    public async ValueTask InitializeAsync()
    {
        await _redisContainer.StartAsync();

        _redisConnectionString =
            _redisContainer.GetConnectionString();

        _factory = new BasketServiceFactory(
            _redisConnectionString,
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
