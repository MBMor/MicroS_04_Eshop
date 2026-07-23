using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrdersService.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrdersService.IntegrationTests.Infrastructure;

public sealed class OrdersServiceFixture
    : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer =
        new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("orders_integration_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private OrdersServiceFactory? _factory;

    public HttpClient Client
    {
        get;
        private set;
    } = null!;

    internal TestBasketClient BasketClient
    {
        get;
    } = new();

    internal OrdersServiceFactory Factory =>
        _factory
        ?? throw new InvalidOperationException(
            "The Orders Service factory has not been initialized.");

    public async ValueTask InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        _factory = new OrdersServiceFactory(
            _postgresContainer.GetConnectionString(),
            BasketClient);

        Client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        await ApplyMigrationsAsync();
    }

    public async ValueTask ResetAsync()
    {
        BasketClient.Reset();

        await using AsyncServiceScope scope =
            Factory.Services.CreateAsyncScope();

        OrdersDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<OrdersDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                order_status_history,
                order_items,
                outbox_messages,
                processed_messages,
                orders
            RESTART IDENTITY CASCADE;
            """);
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        _factory?.Dispose();

        await _postgresContainer.DisposeAsync();
    }

    private async Task ApplyMigrationsAsync()
    {
        await using AsyncServiceScope scope =
            Factory.Services.CreateAsyncScope();

        OrdersDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<OrdersDbContext>();

        await dbContext.Database.MigrateAsync();
    }
}
