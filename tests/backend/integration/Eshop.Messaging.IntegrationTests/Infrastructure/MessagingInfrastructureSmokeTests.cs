using InventoryService.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationsService.Data;
using OrdersService.Data;
using PaymentsService.Data;
using Xunit;

namespace Eshop.Messaging.IntegrationTests.Infrastructure;

[Collection(MessagingTestCollections.System)]
public sealed class MessagingInfrastructureSmokeTests(
    MessagingSystemFixture fixture)
{
    [Fact]
    public async Task InfrastructureAndServiceHostsAreAvailable()
    {
        await Task.WhenAll(
            AssertHealthyAsync(
                fixture.OrdersFactory),
            AssertHealthyAsync(
                fixture.InventoryFactory),
            AssertHealthyAsync(
                fixture.PaymentsFactory),
            AssertHealthyAsync(
                fixture.NotificationsFactory));

        await Task.WhenAll(
            AssertNoPendingMigrationsAsync<OrdersDbContext>(
                fixture.OrdersFactory.Services),
            AssertNoPendingMigrationsAsync<InventoryDbContext>(
                fixture.InventoryFactory.Services),
            AssertNoPendingMigrationsAsync<PaymentsDbContext>(
                fixture.PaymentsFactory.Services),
            AssertNoPendingMigrationsAsync<NotificationsDbContext>(
                fixture.NotificationsFactory.Services));
    }

    private static async Task AssertHealthyAsync<TEntryPoint>(
        WebApplicationFactory<TEntryPoint> factory)
        where TEntryPoint : class
    {
        using HttpClient client =
            factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });

        using HttpResponseMessage response =
            await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }

    private static async Task AssertNoPendingMigrationsAsync<TContext>(
        IServiceProvider serviceProvider)
        where TContext : DbContext
    {
        using IServiceScope scope =
            serviceProvider.CreateScope();

        TContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<TContext>();

        IEnumerable<string> pendingMigrations =
            await dbContext.Database
                .GetPendingMigrationsAsync();

        Assert.Empty(pendingMigrations);
    }
}
