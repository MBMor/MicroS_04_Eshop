using InventoryService.Data;
using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using PaymentsService.Data;

using InventoryOutboxStatus =
    InventoryService.Outbox.OutboxMessageStatus;

using OrdersOutboxStatus =
    OrdersService.Outbox.OutboxMessageStatus;

using PaymentsOutboxStatus =
    PaymentsService.Outbox.OutboxMessageStatus;

namespace Eshop.Messaging.IntegrationTests.Infrastructure;

public static class MessagingTestReset
{
    private static readonly TimeSpan ResetTimeout =
        TimeSpan.FromSeconds(15);

    private static readonly TimeSpan StabilityInterval =
        TimeSpan.FromMilliseconds(300);

    public static async Task ResetAsync(
        MessagingSystemFixture fixture,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        RabbitMqTestAdmin rabbitMqAdmin =
            new(fixture);

        await WaitForSystemToBecomeIdleAsync(
            fixture,
            rabbitMqAdmin,
            cancellationToken);

        fixture.OrdersFactory.BasketClient.Reset();

        await rabbitMqAdmin.PurgeAllAsync(
            cancellationToken);

        await DatabaseTestReset.ResetAsync(
            fixture,
            cancellationToken);

        // Captures a message that may have been acknowledged just
        // before the first purge while the consumer was shutting down.
        await Task.Delay(
            StabilityInterval,
            cancellationToken);

        await rabbitMqAdmin.PurgeAllAsync(
            cancellationToken);
    }

    private static async Task WaitForSystemToBecomeIdleAsync(
        MessagingSystemFixture fixture,
        RabbitMqTestAdmin rabbitMqAdmin,
        CancellationToken cancellationToken)
    {
        await Eventually.UntilAsync(
            async token =>
            {
                bool queuesAreEmpty =
                    await rabbitMqAdmin
                        .MainQueuesAreEmptyAsync(token);

                if (!queuesAreEmpty)
                {
                    return false;
                }

                bool outboxesAreIdle =
                    await OutboxesAreIdleAsync(
                        fixture,
                        token);

                if (!outboxesAreIdle)
                {
                    return false;
                }

                await Task.Delay(
                    StabilityInterval,
                    token);

                return
                    await rabbitMqAdmin
                        .MainQueuesAreEmptyAsync(token)
                    && await OutboxesAreIdleAsync(
                        fixture,
                        token);
            },
            "RabbitMQ queues and outboxes should become idle.",
            timeout: ResetTimeout,
            cancellationToken: cancellationToken);
    }

    private static async Task<bool> OutboxesAreIdleAsync(
        MessagingSystemFixture fixture,
        CancellationToken cancellationToken)
    {
        Task<bool> ordersIdle =
            OrdersOutboxIsIdleAsync(
                fixture,
                cancellationToken);

        Task<bool> inventoryIdle =
            InventoryOutboxIsIdleAsync(
                fixture,
                cancellationToken);

        Task<bool> paymentsIdle =
            PaymentsOutboxIsIdleAsync(
                fixture,
                cancellationToken);

        bool[] results =
            await Task.WhenAll(
                ordersIdle,
                inventoryIdle,
                paymentsIdle);

        return results.All(
            result => result);
    }

    private static Task<bool> OrdersOutboxIsIdleAsync(
        MessagingSystemFixture fixture,
        CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            OrdersDbContext,
            bool>(
            fixture.OrdersFactory.Services,
            async (dbContext, token) =>
            {
                return !await dbContext.OutboxMessages
                    .AnyAsync(
                        message =>
                            message.Status
                                == OrdersOutboxStatus.Pending
                            || message.Status
                                == OrdersOutboxStatus.Processing
                            || message.Status
                                == OrdersOutboxStatus.Failed,
                        token);
            },
            cancellationToken);
    }

    private static Task<bool> InventoryOutboxIsIdleAsync(
        MessagingSystemFixture fixture,
        CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            InventoryDbContext,
            bool>(
            fixture.InventoryFactory.Services,
            async (dbContext, token) =>
            {
                return !await dbContext.OutboxMessages
                    .AnyAsync(
                        message =>
                            message.Status
                                == InventoryOutboxStatus.Pending
                            || message.Status
                                == InventoryOutboxStatus.Processing
                            || message.Status
                                == InventoryOutboxStatus.Failed,
                        token);
            },
            cancellationToken);
    }

    private static Task<bool> PaymentsOutboxIsIdleAsync(
        MessagingSystemFixture fixture,
        CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            PaymentsDbContext,
            bool>(
            fixture.PaymentsFactory.Services,
            async (dbContext, token) =>
            {
                return !await dbContext.OutboxMessages
                    .AnyAsync(
                        message =>
                            message.Status
                                == PaymentsOutboxStatus.Pending
                            || message.Status
                                == PaymentsOutboxStatus.Processing
                            || message.Status
                                == PaymentsOutboxStatus.Failed,
                        token);
            },
            cancellationToken);
    }
}
