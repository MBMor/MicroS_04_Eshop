using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationsService.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace NotificationsService.IntegrationTests.Infrastructure;

public sealed class NotificationsServiceFixture
    : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer =
        new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("notifications_integration_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private NotificationsServiceFactory? _factory;

    public HttpClient Client
    {
        get;
        private set;
    } = null!;

    internal NotificationsServiceFactory Factory =>
        _factory
        ?? throw new InvalidOperationException(
            "The Notifications Service factory has not been initialized.");

    public async ValueTask InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        _factory = new NotificationsServiceFactory(
            _postgresContainer.GetConnectionString());

        Client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        await ApplyMigrationsAsync();
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

        NotificationsDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<NotificationsDbContext>();

        await dbContext.Database.MigrateAsync();
    }
}
