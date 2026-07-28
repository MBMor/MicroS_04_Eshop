using System.Net;
using System.Net.Http.Json;
using Eshop.Messaging.IntegrationTests.Infrastructure;
using Eshop.Messaging.IntegrationTests.Infrastructure.Fakes;
using Eshop.Messaging.RabbitMq;
using InventoryService.Data;
using InventoryService.Domain;
using Microsoft.EntityFrameworkCore;
using NotificationsService.Data;
using NotificationsService.Domain;
using OrdersService.Contracts;
using OrdersService.Data;
using OrdersService.Domain;
using OrdersService.Integration;
using PaymentsService.Application;
using PaymentsService.Data;
using PaymentsService.Domain;
using Xunit;

using InventoryOutboxStatus =
    InventoryService.Outbox.OutboxMessageStatus;

using OrdersOutboxStatus =
    OrdersService.Outbox.OutboxMessageStatus;

using PaymentsOutboxStatus =
    PaymentsService.Outbox.OutboxMessageStatus;

namespace Eshop.Messaging.IntegrationTests.Scenarios;

[Collection(MessagingTestCollections.System)]
public sealed class InventoryConcurrencyMessagingTests(
    MessagingSystemFixture fixture)
    : MessagingIntegrationTestBase(fixture)
{
    private const string FirstCustomerId =
        "inventory-race-customer-1";

    private const string SecondCustomerId =
        "inventory-race-customer-2";

    private const string Currency =
        "CZK";

    private static readonly TimeSpan ScenarioTimeout =
        TimeSpan.FromSeconds(45);

    [Fact]
    public async Task
        ConcurrentOrderCreatedDeliveriesForLastUnitDoNotOversellOrDeadLetter()
    {
        Guid productId =
            Guid.NewGuid();

        await SeedInventoryAsync(
            productId,
            initialStock: 1);

        SetBasket(
            FirstCustomerId,
            productId);

        SetBasket(
            SecondCustomerId,
            productId);

        CoordinatedInventorySaveInterceptor coordinator =
            new(requiredParticipants: 2);

        await Fixture.UseTwoCoordinatedInventoryConsumersAsync(
            coordinator);

        try
        {
            OrderResponse[] orders =
                await Task.WhenAll(
                    CreateOrderAsync(
                        FirstCustomerId,
                        "inventory-race-1@example.test"),
                    CreateOrderAsync(
                        SecondCustomerId,
                        "inventory-race-2@example.test"));

            Assert.Equal(
                2,
                orders
                    .Select(order => order.Id)
                    .Distinct()
                    .Count());

            (Guid confirmedOrderId,
                Guid failedOrderId) =
                await AssertOneConfirmedAndOneFailedAsync(
                    orders.Select(order => order.Id).ToArray());

            Assert.Equal(
                2,
                coordinator.FirstWaveArrivals);

            Assert.True(
                coordinator.SaveAttemptCount >= 3,
                "The losing reservation should retry after " +
                "the synchronized PostgreSQL conflict.");

            await AssertInventoryAndMessagingCardinalityAsync(
                productId,
                confirmedOrderId,
                failedOrderId);

            await AssertMessagingQueuesAreEmptyAsync();
        }
        finally
        {
            coordinator.Release();

            await Fixture.RestoreSingleInventoryConsumerAsync();
        }
    }

    private Task SeedInventoryAsync(
        Guid productId,
        int initialStock)
    {
        return DatabaseTestScope.ExecuteAsync<
            InventoryDbContext>(
            Fixture.InventoryFactory.Services,
            async (dbContext, cancellationToken) =>
            {
                InventoryItem inventoryItem =
                    InventoryItem.Create(
                        id: Guid.NewGuid(),
                        productId,
                        sku: $"RACE-{productId:N}",
                        initialOnHandQuantity:
                            initialStock,
                        isActive: true,
                        createdAtUtc:
                            DateTimeOffset.UtcNow);

                dbContext.InventoryItems.Add(
                    inventoryItem);

                await dbContext.SaveChangesAsync(
                    cancellationToken);
            });
    }

    private void SetBasket(
        string customerId,
        Guid productId)
    {
        const decimal unitPrice = 49.90m;

        Fixture.OrdersFactory.BasketClient.SetBasket(
            customerId,
            new BasketSnapshot(
            [
                new BasketItemSnapshot(
                    ProductId: productId,
                    ProductName:
                        "Last Unit Race Product",
                    UnitPrice: unitPrice,
                    Currency,
                    Quantity: 1,
                    LineTotal: unitPrice)
            ]));
    }

    private async Task<OrderResponse> CreateOrderAsync(
        string customerId,
        string customerEmail)
    {
        using HttpClient client =
            Fixture.OrdersFactory.CreateClient();

        client.DefaultRequestHeaders.Add(
            TestOrderOwnerProvider.CustomerIdHeaderName,
            customerId);

        client.DefaultRequestHeaders.Add(
            OrderHeaders.IdempotencyKey,
            Guid.NewGuid().ToString());

        CreateOrderRequest request = new()
        {
            CustomerEmail = customerEmail,
            PaymentMethod =
                FakePaymentProcessor.SuccessMethod
        };

        using HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/api/v1/orders",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        OrderResponse? order =
            await response.Content
                .ReadFromJsonAsync<OrderResponse>();

        return Assert.IsType<OrderResponse>(
            order);
    }

    private async Task<(Guid ConfirmedOrderId,
        Guid FailedOrderId)>
        AssertOneConfirmedAndOneFailedAsync(
            Guid[] orderIds)
    {
        Guid confirmedOrderId =
            Guid.Empty;

        Guid failedOrderId =
            Guid.Empty;

        await Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                OrderState[] states =
                    await DatabaseTestScope.ExecuteAsync<
                        OrdersDbContext,
                        OrderState[]>(
                        Fixture.OrdersFactory.Services,
                        async (dbContext, token) =>
                        {
                            return await dbContext.Orders
                                .AsNoTracking()
                                .Where(order =>
                                    orderIds.Contains(order.Id))
                                .Select(order =>
                                    new OrderState(
                                        order.Id,
                                        order.Status,
                                        order.StatusHistory.Count))
                                .ToArrayAsync(token);
                        },
                        cancellationToken);

                Assert.Equal(2, states.Length);

                OrderState confirmed =
                    Assert.Single(
                        states,
                        state =>
                            state.Status
                                == OrderStatus.Confirmed);

                OrderState failed =
                    Assert.Single(
                        states,
                        state =>
                            state.Status
                                == OrderStatus
                                    .StockReservationFailed);

                Assert.Equal(3, confirmed.HistoryCount);
                Assert.Equal(2, failed.HistoryCount);

                confirmedOrderId = confirmed.Id;
                failedOrderId = failed.Id;
            },
            "Exactly one competing order should confirm and " +
            "one should fail stock reservation.",
            timeout: ScenarioTimeout);

        return (
            confirmedOrderId,
            failedOrderId);
    }

    private Task AssertInventoryAndMessagingCardinalityAsync(
        Guid productId,
        Guid confirmedOrderId,
        Guid failedOrderId)
    {
        return Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                await AssertInventoryEffectsAsync(
                    productId,
                    cancellationToken);

                await AssertOrdersEffectsAsync(
                    cancellationToken);

                await AssertPaymentEffectsAsync(
                    confirmedOrderId,
                    cancellationToken);

                await AssertNotificationEffectsAsync(
                    confirmedOrderId,
                    failedOrderId,
                    cancellationToken);
            },
            "The broker-delivered inventory race should " +
            "produce one coherent winner and loser workflow.",
            timeout: ScenarioTimeout);
    }

    private Task AssertInventoryEffectsAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            InventoryDbContext>(
            Fixture.InventoryFactory.Services,
            async (dbContext, token) =>
            {
                InventorySnapshot inventory =
                    await dbContext.InventoryItems
                        .AsNoTracking()
                        .Where(item =>
                            item.ProductId == productId)
                        .Select(item =>
                            new InventorySnapshot(
                                item.OnHandQuantity,
                                item.ReservedQuantity))
                        .SingleAsync(token);

                Assert.Equal(1, inventory.OnHandQuantity);
                Assert.Equal(1, inventory.ReservedQuantity);
                Assert.Equal(0, inventory.AvailableQuantity);

                Assert.Equal(
                    2,
                    await dbContext.ProcessedMessages
                        .CountAsync(token));

                Assert.Equal(
                    2,
                    await dbContext.OutboxMessages
                        .CountAsync(token));

                Assert.Equal(
                    2,
                    await dbContext.OutboxMessages
                        .CountAsync(
                            message =>
                                message.Status
                                    == InventoryOutboxStatus
                                        .Published,
                            token));

                string[] routingKeys =
                    await dbContext.OutboxMessages
                        .AsNoTracking()
                        .OrderBy(message =>
                            message.RoutingKey)
                        .Select(message =>
                            message.RoutingKey)
                        .ToArrayAsync(token);

                Assert.Equal(
                    [
                        RabbitMqRoutingKeys
                            .StockReservationFailedV1,
                        RabbitMqRoutingKeys.StockReservedV1
                    ],
                    routingKeys);
            },
            cancellationToken);
    }

    private Task AssertOrdersEffectsAsync(
        CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            OrdersDbContext>(
            Fixture.OrdersFactory.Services,
            async (dbContext, token) =>
            {
                Assert.Equal(
                    3,
                    await dbContext.ProcessedMessages
                        .CountAsync(token));

                Assert.Equal(
                    4,
                    await dbContext.OutboxMessages
                        .CountAsync(token));

                Assert.Equal(
                    4,
                    await dbContext.OutboxMessages
                        .CountAsync(
                            message =>
                                message.Status
                                    == OrdersOutboxStatus
                                        .Published,
                            token));
            },
            cancellationToken);
    }

    private Task AssertPaymentEffectsAsync(
        Guid confirmedOrderId,
        CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            PaymentsDbContext>(
            Fixture.PaymentsFactory.Services,
            async (dbContext, token) =>
            {
                PaymentSnapshot payment =
                    await dbContext.Payments
                        .AsNoTracking()
                        .Select(payment =>
                            new PaymentSnapshot(
                                payment.OrderId,
                                payment.Status))
                        .SingleAsync(token);

                Assert.Equal(
                    confirmedOrderId,
                    payment.OrderId);

                Assert.Equal(
                    PaymentStatus.Authorized,
                    payment.Status);

                Assert.Equal(
                    1,
                    await dbContext.ProcessedMessages
                        .CountAsync(token));

                Assert.Equal(
                    1,
                    await dbContext.OutboxMessages
                        .CountAsync(token));

                Assert.Equal(
                    1,
                    await dbContext.OutboxMessages
                        .CountAsync(
                            message =>
                                message.Status
                                    == PaymentsOutboxStatus
                                        .Published,
                            token));
            },
            cancellationToken);
    }

    private Task AssertNotificationEffectsAsync(
        Guid confirmedOrderId,
        Guid failedOrderId,
        CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            NotificationsDbContext>(
            Fixture.NotificationsFactory.Services,
            async (dbContext, token) =>
            {
                NotificationSnapshot[] notifications =
                    await dbContext.Notifications
                        .AsNoTracking()
                        .Where(notification =>
                            notification.OrderId
                                == confirmedOrderId
                            || notification.OrderId
                                == failedOrderId)
                        .Select(notification =>
                            new NotificationSnapshot(
                                notification.OrderId,
                                notification.Type))
                        .ToArrayAsync(token);

                Assert.Equal(6, notifications.Length);

                NotificationType[] confirmedTypes =
                    notifications
                        .Where(notification =>
                            notification.OrderId
                                == confirmedOrderId)
                        .Select(notification =>
                            notification.Type)
                        .ToArray();

                Assert.Equal(4, confirmedTypes.Length);
                Assert.Contains(
                    NotificationType.OrderCreated,
                    confirmedTypes);
                Assert.Contains(
                    NotificationType.StockReserved,
                    confirmedTypes);
                Assert.Contains(
                    NotificationType.PaymentAuthorized,
                    confirmedTypes);
                Assert.Contains(
                    NotificationType.OrderConfirmed,
                    confirmedTypes);

                NotificationType[] failedTypes =
                    notifications
                        .Where(notification =>
                            notification.OrderId
                                == failedOrderId)
                        .Select(notification =>
                            notification.Type)
                        .ToArray();

                Assert.Equal(2, failedTypes.Length);
                Assert.Contains(
                    NotificationType.OrderCreated,
                    failedTypes);
                Assert.Contains(
                    NotificationType.StockReservationFailed,
                    failedTypes);

                Assert.Equal(
                    6,
                    await dbContext.ProcessedMessages
                        .CountAsync(token));
            },
            cancellationToken);
    }

    private async Task AssertMessagingQueuesAreEmptyAsync()
    {
        RabbitMqTestAdmin rabbitMqAdmin =
            new(Fixture);

        await Eventually.UntilAsync(
            async cancellationToken =>
            {
                Dictionary<string, uint> counts =
                    await rabbitMqAdmin
                        .GetReadyMessageCountsAsync(
                            includeDeadLetterQueues: true,
                            cancellationToken);

                return counts.Values.All(
                    count => count == 0);
            },
            "All RabbitMQ main and dead-letter queues " +
            "should be empty after the inventory race.",
            timeout: ScenarioTimeout);

        await Task.Delay(
            TimeSpan.FromMilliseconds(300));

        Dictionary<string, uint> stableCounts =
            await rabbitMqAdmin.GetReadyMessageCountsAsync(
                includeDeadLetterQueues: true);

        Assert.All(
            stableCounts,
            queue => Assert.Equal(0u, queue.Value));
    }

    private sealed record OrderState(
        Guid Id,
        OrderStatus Status,
        int HistoryCount);

    private sealed record InventorySnapshot(
        int OnHandQuantity,
        int ReservedQuantity)
    {
        public int AvailableQuantity =>
            OnHandQuantity - ReservedQuantity;
    }

    private sealed record PaymentSnapshot(
        Guid OrderId,
        PaymentStatus Status);

    private sealed record NotificationSnapshot(
        Guid? OrderId,
        NotificationType Type);
}
