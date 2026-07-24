using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentsService.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace PaymentsService.IntegrationTests.Infrastructure;

public sealed class PaymentsServiceFixture
    : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer =
        new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("payments_integration_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private PaymentsServiceFactory? _factory;

    public HttpClient Client
    {
        get;
        private set;
    } = null!;

    internal PaymentsServiceFactory Factory =>
        _factory
        ?? throw new InvalidOperationException(
            "The Payments Service factory has not been initialized.");

    public async ValueTask InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        _factory = new PaymentsServiceFactory(
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

        PaymentsDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<PaymentsDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                outbox_messages,
                processed_messages,
                payments
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

        PaymentsDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<PaymentsDbContext>();

        await dbContext.Database.MigrateAsync();
    }
}
