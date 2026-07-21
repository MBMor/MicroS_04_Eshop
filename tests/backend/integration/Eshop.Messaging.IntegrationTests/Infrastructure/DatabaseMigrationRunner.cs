using Eshop.Messaging.IntegrationTests.Infrastructure.Factories;
using InventoryService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationsService.Data;
using OrdersService.Data;
using PaymentsService.Data;

namespace Eshop.Messaging.IntegrationTests.Infrastructure;

public static class DatabaseMigrationRunner
{
    public static async Task ApplyAsync(
        MessagingSystemFixture fixture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await ApplyOrdersMigrationsAsync(
            fixture,
            cancellationToken);

        await ApplyInventoryMigrationsAsync(
            fixture,
            cancellationToken);

        await ApplyPaymentsMigrationsAsync(
            fixture,
            cancellationToken);

        await ApplyNotificationsMigrationsAsync(
            fixture,
            cancellationToken);
    }

    private static async Task ApplyOrdersMigrationsAsync(
        MessagingSystemFixture fixture,
        CancellationToken cancellationToken)
    {
        using OrdersServiceFactory factory =
            new(
                fixture,
                suppressHostedServices: true);

        await ApplyAndVerifyAsync<OrdersDbContext>(
            factory.Services,
            "Orders",
            cancellationToken);
    }

    private static async Task ApplyInventoryMigrationsAsync(
        MessagingSystemFixture fixture,
        CancellationToken cancellationToken)
    {
        using InventoryServiceFactory factory =
            new(
                fixture,
                suppressHostedServices: true);

        await ApplyAndVerifyAsync<InventoryDbContext>(
            factory.Services,
            "Inventory",
            cancellationToken);
    }

    private static async Task ApplyPaymentsMigrationsAsync(
        MessagingSystemFixture fixture,
        CancellationToken cancellationToken)
    {
        using PaymentsServiceFactory factory =
            new(
                fixture,
                suppressHostedServices: true);

        await ApplyAndVerifyAsync<PaymentsDbContext>(
            factory.Services,
            "Payments",
            cancellationToken);
    }

    private static async Task ApplyNotificationsMigrationsAsync(
        MessagingSystemFixture fixture,
        CancellationToken cancellationToken)
    {
        using NotificationsServiceFactory factory =
            new(
                fixture,
                suppressHostedServices: true);

        await ApplyAndVerifyAsync<NotificationsDbContext>(
            factory.Services,
            "Notifications",
            cancellationToken);
    }

    private static async Task ApplyAndVerifyAsync<TContext>(
        IServiceProvider serviceProvider,
        string serviceName,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        using IServiceScope scope =
            serviceProvider.CreateScope();

        TContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<TContext>();

        await dbContext.Database.MigrateAsync(
            cancellationToken);

        string[] pendingMigrations =
            (await dbContext.Database
                .GetPendingMigrationsAsync(
                    cancellationToken))
            .ToArray();

        if (pendingMigrations.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{serviceName} database still has pending " +
            $"migrations after MigrateAsync: " +
            $"{string.Join(", ", pendingMigrations)}.");
    }
}
