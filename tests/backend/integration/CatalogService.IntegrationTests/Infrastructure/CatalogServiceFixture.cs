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
    private string? _postgresConnectionString;

    public HttpClient Client
    {
        get;
        private set;
    } = null!;

    internal CatalogServiceFactory Factory =>
        _factory
        ?? throw new InvalidOperationException(
            "The Catalog Service factory has not been initialized.");

    internal string PostgresConnectionString =>
        _postgresConnectionString
        ?? throw new InvalidOperationException(
            "The PostgreSQL connection string has not been initialized.");

    public async ValueTask InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        _postgresConnectionString =
            _postgresContainer.GetConnectionString();

        _factory = new CatalogServiceFactory(
            _postgresConnectionString);

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

    public Task PausePostgresAsync(
        CancellationToken cancellationToken = default)
    {
        return _postgresContainer.PauseAsync(
            cancellationToken);
    }

    public Task UnpausePostgresAsync(
        CancellationToken cancellationToken = default)
    {
        return _postgresContainer.UnpauseAsync(
            cancellationToken);
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
