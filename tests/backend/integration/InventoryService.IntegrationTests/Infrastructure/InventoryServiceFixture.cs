using InventoryService.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace InventoryService.IntegrationTests.Infrastructure;

public sealed class InventoryServiceFixture
    : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer =
        new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("inventory_integration_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private InventoryServiceFactory? _factory;

    public HttpClient Client
    {
        get;
        private set;
    } = null!;

    internal InventoryServiceFactory Factory =>
        _factory
        ?? throw new InvalidOperationException(
            "The Inventory Service factory has not been initialized.");

    public async ValueTask InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        _factory = new InventoryServiceFactory(
            _postgresContainer.GetConnectionString());

        Client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        await ApplyMigrationsAsync();
    }

    public async ValueTask ResetDatabaseAsync()
    {
        await using AsyncServiceScope scope =
            Factory.Services.CreateAsyncScope();

        InventoryDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                outbox_messages,
                processed_messages,
                inventory_items
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

        InventoryDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

        await dbContext.Database.MigrateAsync();
    }
}
