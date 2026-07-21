using System.Net;
using System.Net.Http.Json;
using Eshop.Messaging.IntegrationTests.Infrastructure;
using Eshop.Messaging.IntegrationTests.Infrastructure.Fakes;
using InventoryService.Data;
using InventoryService.Domain;
using Messaging.Shared.RabbitMq;
using Microsoft.EntityFrameworkCore;
using NotificationsService.Data;
using NotificationsService.Domain;
using OrdersService.Contracts;
using OrdersService.Data;
using OrdersService.Domain;
using OrdersService.Integration;
using PaymentsService.Application;
using PaymentsService.Data;
using Xunit;

using InventoryOutboxStatus =
    InventoryService.Outbox.OutboxMessageStatus;

using OrdersOutboxStatus =
    OrdersService.Outbox.OutboxMessageStatus;

namespace Eshop.Messaging.IntegrationTests.Scenarios;

[Collection(MessagingTestCollections.System)]
public sealed class OrderStockFailureMessagingTests(
    MessagingSystemFixture fixture)
    : MessagingIntegrationTestBase(fixture)
{
    private const string CustomerId =
        "stock-failure-customer";

    private const string CustomerEmail =
        "stock-failure@example.test";

    private const string Currency =
        "CZK";

    private const string ExpectedFailureReason =
        "One or more order items could not be reserved.";

    private static readonly TimeSpan ScenarioTimeout =
        TimeSpan.FromSeconds(30);

    [Fact]
    public async Task CreateOrder_WithInsufficientStock_MarksReservationAsFailed()
    {
        Guid productId =
            Guid.NewGuid();

        const int initialStock = 1;
        const int orderedQuantity = 2;
        const decimal unitPrice = 49.90m;

        await SeedInventoryAsync(
            productId,
            initialStock);

        Fixture.OrdersFactory.BasketClient.SetBasket(
            CustomerId,
            new BasketSnapshot(
            [
                new BasketItemSnapshot(
                    ProductId: productId,
                    ProductName:
                        "Insufficient Stock Test Product",
                    UnitPrice: unitPrice,
                    Currency: Currency,
                    Quantity: orderedQuantity,
                    LineTotal:
                        unitPrice * orderedQuantity)
            ]));

        OrderResponse createdOrder =
            await CreateOrderAsync();

        Assert.NotEqual(
            Guid.Empty,
            createdOrder.Id);

        Assert.Equal(
            OrderStatus.PendingStockReservation.ToString(),
            createdOrder.Status);

        Assert.Single(
            createdOrder.Items);

        Assert.True(
            Fixture.OrdersFactory.BasketClient.WasCleared(
                CustomerId));

        await AssertOrderFailedAsync(
            createdOrder.Id);

        await AssertInventoryWasNotReservedAsync(
            productId,
            initialStock);

        await AssertFailureNotificationsAsync(
            createdOrder.Id);

        await AssertOutboxMessagesAsync();

        await AssertNoPaymentWasCreatedAsync(
            createdOrder.Id);

        await AssertMessagingQueuesAreEmptyAsync();
    }

    private async Task<OrderResponse> CreateOrderAsync()
    {
        using HttpClient client =
            Fixture.OrdersFactory.CreateClient();

        client.DefaultRequestHeaders.Add(
            TestOrderOwnerProvider.CustomerIdHeaderName,
            CustomerId);

        CreateOrderRequest request = new()
        {
            CustomerEmail = CustomerEmail,
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
                        productId: productId,
                        sku:
                            $"TEST-{productId:N}",
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

    private Task AssertOrderFailedAsync(
        Guid orderId)
    {
        return Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                OrderSnapshot order =
                    await LoadOrderAsync(
                        orderId,
                        cancellationToken);

                Assert.Equal(
                    OrderStatus.StockReservationFailed,
                    order.Status);

                Assert.Collection(
                    order.StatusHistory,
                    history =>
                    {
                        Assert.Null(
                            history.FromStatus);

                        Assert.Equal(
                            OrderStatus.PendingStockReservation,
                            history.ToStatus);

                        Assert.Equal(
                            "Order created and awaiting stock reservation.",
                            history.Reason);
                    },
                    history =>
                    {
                        Assert.Equal(
                            OrderStatus.PendingStockReservation,
                            history.FromStatus);

                        Assert.Equal(
                            OrderStatus.StockReservationFailed,
                            history.ToStatus);

                        Assert.Equal(
                            ExpectedFailureReason,
                            history.Reason);
                    });
            },
            $"Order '{orderId}' should fail stock reservation.",
            timeout: ScenarioTimeout);
    }

    private Task<OrderSnapshot> LoadOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            OrdersDbContext,
            OrderSnapshot>(
            Fixture.OrdersFactory.Services,
            async (dbContext, token) =>
            {
                OrderStatus status =
                    await dbContext.Orders
                        .AsNoTracking()
                        .Where(order =>
                            order.Id == orderId)
                        .Select(order =>
                            order.Status)
                        .SingleAsync(token);

                OrderStatusHistorySnapshot[] history =
                    await dbContext.OrderStatusHistories
                        .AsNoTracking()
                        .Where(entry =>
                            entry.OrderId == orderId)
                        .OrderBy(entry =>
                            entry.ChangedAtUtc)
                        .ThenBy(entry =>
                            entry.Id)
                        .Select(entry =>
                            new OrderStatusHistorySnapshot(
                                entry.FromStatus,
                                entry.ToStatus,
                                entry.Reason))
                        .ToArrayAsync(token);

                return new OrderSnapshot(
                    status,
                    history);
            },
            cancellationToken);
    }

    private Task AssertInventoryWasNotReservedAsync(
        Guid productId,
        int expectedOnHandQuantity)
    {
        return Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                InventorySnapshot inventory =
                    await DatabaseTestScope.ExecuteAsync<
                        InventoryDbContext,
                        InventorySnapshot>(
                        Fixture.InventoryFactory.Services,
                        async (dbContext, token) =>
                        {
                            return await dbContext.InventoryItems
                                .AsNoTracking()
                                .Where(item =>
                                    item.ProductId == productId)
                                .Select(item =>
                                    new InventorySnapshot(
                                        item.OnHandQuantity,
                                        item.ReservedQuantity))
                                .SingleAsync(token);
                        },
                        cancellationToken);

                Assert.Equal(
                    expectedOnHandQuantity,
                    inventory.OnHandQuantity);

                Assert.Equal(
                    0,
                    inventory.ReservedQuantity);

                Assert.Equal(
                    expectedOnHandQuantity,
                    inventory.AvailableQuantity);
            },
            $"Inventory for product '{productId}' should remain unchanged.",
            timeout: ScenarioTimeout);
    }

    private Task AssertFailureNotificationsAsync(
        Guid orderId)
    {
        return Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                NotificationType[] notificationTypes =
                    await DatabaseTestScope.ExecuteAsync<
                        NotificationsDbContext,
                        NotificationType[]>(
                        Fixture.NotificationsFactory.Services,
                        async (dbContext, token) =>
                        {
                            return await dbContext.Notifications
                                .AsNoTracking()
                                .Where(notification =>
                                    notification.OrderId
                                        == orderId)
                                .OrderBy(notification =>
                                    notification.CreatedAtUtc)
                                .ThenBy(notification =>
                                    notification.Id)
                                .Select(notification =>
                                    notification.Type)
                                .ToArrayAsync(token);
                        },
                        cancellationToken);

                Assert.Equal(
                    2,
                    notificationTypes.Length);

                Assert.Contains(
                    NotificationType.OrderCreated,
                    notificationTypes);

                Assert.Contains(
                    NotificationType.StockReservationFailed,
                    notificationTypes);

                Assert.DoesNotContain(
                    NotificationType.StockReserved,
                    notificationTypes);

                Assert.DoesNotContain(
                    NotificationType.PaymentAuthorized,
                    notificationTypes);

                Assert.DoesNotContain(
                    NotificationType.OrderConfirmed,
                    notificationTypes);
            },
            $"Stock-failure notifications for order '{orderId}' should be created.",
            timeout: ScenarioTimeout);
    }

    private Task AssertOutboxMessagesAsync()
    {
        return Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                OutboxSnapshot ordersOutbox =
                    await LoadOrdersOutboxAsync(
                        cancellationToken);

                Assert.Equal(
                    1,
                    ordersOutbox.Count);

                Assert.Equal(
                    1,
                    ordersOutbox.PublishedCount);

                string ordersRoutingKey =
                    Assert.Single(
                        ordersOutbox.RoutingKeys);

                Assert.Equal(
                    RabbitMqRoutingKeys.OrderCreatedV1,
                    ordersRoutingKey);

                OutboxSnapshot inventoryOutbox =
                    await LoadInventoryOutboxAsync(
                        cancellationToken);

                Assert.Equal(
                    1,
                    inventoryOutbox.Count);

                Assert.Equal(
                    1,
                    inventoryOutbox.PublishedCount);

                string inventoryRoutingKey =
                    Assert.Single(
                        inventoryOutbox.RoutingKeys);

                Assert.Equal(
                    RabbitMqRoutingKeys
                        .StockReservationFailedV1,
                    inventoryRoutingKey);
            },
            "Stock-failure outbox messages should be published.",
            timeout: ScenarioTimeout);
    }

    private Task<OutboxSnapshot> LoadOrdersOutboxAsync(
        CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            OrdersDbContext,
            OutboxSnapshot>(
            Fixture.OrdersFactory.Services,
            async (dbContext, token) =>
            {
                int count =
                    await dbContext.OutboxMessages
                        .CountAsync(token);

                int publishedCount =
                    await dbContext.OutboxMessages
                        .CountAsync(
                            message =>
                                message.Status
                                    == OrdersOutboxStatus.Published,
                            token);

                string[] routingKeys =
                    await dbContext.OutboxMessages
                        .AsNoTracking()
                        .OrderBy(message =>
                            message.OccurredAtUtc)
                        .Select(message =>
                            message.RoutingKey)
                        .ToArrayAsync(token);

                return new OutboxSnapshot(
                    count,
                    publishedCount,
                    routingKeys);
            },
            cancellationToken);
    }

    private Task<OutboxSnapshot> LoadInventoryOutboxAsync(
        CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            InventoryDbContext,
            OutboxSnapshot>(
            Fixture.InventoryFactory.Services,
            async (dbContext, token) =>
            {
                int count =
                    await dbContext.OutboxMessages
                        .CountAsync(token);

                int publishedCount =
                    await dbContext.OutboxMessages
                        .CountAsync(
                            message =>
                                message.Status
                                    == InventoryOutboxStatus.Published,
                            token);

                string[] routingKeys =
                    await dbContext.OutboxMessages
                        .AsNoTracking()
                        .OrderBy(message =>
                            message.OccurredAtUtc)
                        .Select(message =>
                            message.RoutingKey)
                        .ToArrayAsync(token);

                return new OutboxSnapshot(
                    count,
                    publishedCount,
                    routingKeys);
            },
            cancellationToken);
    }

    private Task AssertNoPaymentWasCreatedAsync(
        Guid orderId)
    {
        return DatabaseTestScope.ExecuteAsync<
            PaymentsDbContext>(
            Fixture.PaymentsFactory.Services,
            async (dbContext, cancellationToken) =>
            {
                bool paymentExists =
                    await dbContext.Payments
                        .AsNoTracking()
                        .AnyAsync(
                            payment =>
                                payment.OrderId == orderId,
                            cancellationToken);

                int paymentOutboxCount =
                    await dbContext.OutboxMessages
                        .CountAsync(
                            cancellationToken);

                int processedMessageCount =
                    await dbContext.ProcessedMessages
                        .CountAsync(
                            cancellationToken);

                Assert.False(
                    paymentExists);

                Assert.Equal(
                    0,
                    paymentOutboxCount);

                Assert.Equal(
                    0,
                    processedMessageCount);
            });
    }

    private Task AssertMessagingQueuesAreEmptyAsync()
    {
        RabbitMqTestAdmin rabbitMqAdmin =
            new(Fixture);

        return Eventually.UntilAsync(
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
            "All RabbitMQ main and dead-letter queues should be empty.",
            timeout: ScenarioTimeout);
    }

    private sealed record OrderSnapshot(
        OrderStatus Status,
        OrderStatusHistorySnapshot[] StatusHistory);

    private sealed record OrderStatusHistorySnapshot(
        OrderStatus? FromStatus,
        OrderStatus ToStatus,
        string Reason);

    private sealed record InventorySnapshot(
        int OnHandQuantity,
        int ReservedQuantity)
    {
        public int AvailableQuantity =>
            OnHandQuantity - ReservedQuantity;
    }

    private sealed record OutboxSnapshot(
        int Count,
        int PublishedCount,
        string[] RoutingKeys);
}
