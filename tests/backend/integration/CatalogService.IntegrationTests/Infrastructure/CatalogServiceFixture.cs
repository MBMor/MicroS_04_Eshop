using CatalogService.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace CatalogService.IntegrationTests.Infrastructure;

public sealed class CatalogServiceFixture
    : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer =
        new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("catalog_integration_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private CatalogServiceFactory? _factory;

    public HttpClient Client
    {
        get;
        private set;
    } = null!;

    internal CatalogServiceFactory Factory =>
        _factory
        ?? throw new InvalidOperationException(
            "The Catalog Service factory has not been initialized.");

    public async ValueTask InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        _factory = new CatalogServiceFactory(
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

        CatalogDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<CatalogDbContext>();

        await dbContext.Products.ExecuteDeleteAsync();
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

        CatalogDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<CatalogDbContext>();

        await dbContext.Database.MigrateAsync();
    }
}
