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
public sealed class OrderPaymentFailureCompensationTests(
    MessagingSystemFixture fixture)
    : MessagingIntegrationTestBase(fixture)
{
    private const string CustomerId =
        "payment-failure-customer";

    private const string CustomerEmail =
        "payment-failure@example.test";

    private const string Currency =
        "CZK";

    private const string PaymentFailureReason =
        "Simulated payment failure.";

    private const string CancellationReason =
        "Payment failed and reserved stock was released.";

    private static readonly TimeSpan ScenarioTimeout =
        TimeSpan.FromSeconds(30);

    [Fact]
    public async Task CreateOrder_WhenPaymentFails_ReleasesStockAndCancelsOrder()
    {
        Guid productId =
            Guid.NewGuid();

        const int initialStock = 10;
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
                        "Payment Failure Test Product",
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

        await AssertOrderWasCancelledAsync(
            createdOrder.Id);

        await AssertFailedPaymentAsync(
            createdOrder.Id,
            unitPrice * orderedQuantity);

        await AssertStockWasReleasedAsync(
            productId,
            initialStock);

        await AssertCompensationNotificationsAsync(
            createdOrder.Id);

        await AssertOutboxMessagesAsync();

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
                FakePaymentProcessor.FailureMethod
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

    private Task AssertOrderWasCancelledAsync(
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
                    OrderStatus.Cancelled,
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
                            OrderStatus.PendingPayment,
                            history.ToStatus);

                        Assert.Equal(
                            "Stock reserved; order is awaiting payment.",
                            history.Reason);
                    },
                    history =>
                    {
                        Assert.Equal(
                            OrderStatus.PendingPayment,
                            history.FromStatus);

                        Assert.Equal(
                            OrderStatus.PaymentFailed,
                            history.ToStatus);

                        Assert.Equal(
                            PaymentFailureReason,
                            history.Reason);
                    },
                    history =>
                    {
                        Assert.Equal(
                            OrderStatus.PaymentFailed,
                            history.FromStatus);

                        Assert.Equal(
                            OrderStatus.Cancelled,
                            history.ToStatus);

                        Assert.Equal(
                            CancellationReason,
                            history.Reason);
                    });
            },
            $"Order '{orderId}' should be cancelled after payment failure.",
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

    private Task AssertFailedPaymentAsync(
        Guid orderId,
        decimal expectedAmount)
    {
        return Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                PaymentSnapshot payment =
                    await DatabaseTestScope.ExecuteAsync<
                        PaymentsDbContext,
                        PaymentSnapshot>(
                        Fixture.PaymentsFactory.Services,
                        async (dbContext, token) =>
                        {
                            return await dbContext.Payments
                                .AsNoTracking()
                                .Where(payment =>
                                    payment.OrderId == orderId)
                                .Select(payment =>
                                    new PaymentSnapshot(
                                        payment.Status,
                                        payment.Amount,
                                        payment.Currency,
                                        payment.PaymentMethod,
                                        payment.FailureReason,
                                        payment.ProcessedAtUtc))
                                .SingleAsync(token);
                        },
                        cancellationToken);

                Assert.Equal(
                    PaymentStatus.Failed,
                    payment.Status);

                Assert.Equal(
                    expectedAmount,
                    payment.Amount);

                Assert.Equal(
                    Currency,
                    payment.Currency);

                Assert.Equal(
                    FakePaymentProcessor.FailureMethod,
                    payment.PaymentMethod);

                Assert.Equal(
                    PaymentFailureReason,
                    payment.FailureReason);

                Assert.NotNull(
                    payment.ProcessedAtUtc);
            },
            $"Payment for order '{orderId}' should fail.",
            timeout: ScenarioTimeout);
    }

    private Task AssertStockWasReleasedAsync(
        Guid productId,
        int initialStock)
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
                    initialStock,
                    inventory.OnHandQuantity);

                Assert.Equal(
                    0,
                    inventory.ReservedQuantity);

                Assert.Equal(
                    initialStock,
                    inventory.AvailableQuantity);
            },
            $"Reserved stock for product '{productId}' should be released.",
            timeout: ScenarioTimeout);
    }

    private Task AssertCompensationNotificationsAsync(
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
                    4,
                    notificationTypes.Length);

                Assert.Contains(
                    NotificationType.OrderCreated,
                    notificationTypes);

                Assert.Contains(
                    NotificationType.StockReserved,
                    notificationTypes);

                Assert.Contains(
                    NotificationType.PaymentFailed,
                    notificationTypes);

                Assert.Contains(
                    NotificationType.OrderCancelled,
                    notificationTypes);

                Assert.DoesNotContain(
                    NotificationType.PaymentAuthorized,
                    notificationTypes);

                Assert.DoesNotContain(
                    NotificationType.OrderConfirmed,
                    notificationTypes);

                Assert.DoesNotContain(
                    NotificationType.StockReservationFailed,
                    notificationTypes);
            },
            $"Compensation notifications for order '{orderId}' should be created.",
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
                    4,
                    ordersOutbox.Count);

                Assert.Equal(
                    4,
                    ordersOutbox.PublishedCount);

                Assert.Contains(
                    RabbitMqRoutingKeys.OrderCreatedV1,
                    ordersOutbox.RoutingKeys);

                Assert.Contains(
                    RabbitMqRoutingKeys.PaymentRequestedV1,
                    ordersOutbox.RoutingKeys);

                Assert.Contains(
                    RabbitMqRoutingKeys.StockReleaseRequestedV1,
                    ordersOutbox.RoutingKeys);

                Assert.Contains(
                    RabbitMqRoutingKeys.OrderCancelledV1,
                    ordersOutbox.RoutingKeys);

                Assert.DoesNotContain(
                    RabbitMqRoutingKeys.OrderConfirmedV1,
                    ordersOutbox.RoutingKeys);

                OutboxSnapshot inventoryOutbox =
                    await LoadInventoryOutboxAsync(
                        cancellationToken);

                Assert.Equal(
                    2,
                    inventoryOutbox.Count);

                Assert.Equal(
                    2,
                    inventoryOutbox.PublishedCount);

                Assert.Contains(
                    RabbitMqRoutingKeys.StockReservedV1,
                    inventoryOutbox.RoutingKeys);

                Assert.Contains(
                    RabbitMqRoutingKeys.StockReleasedV1,
                    inventoryOutbox.RoutingKeys);

                Assert.DoesNotContain(
                    RabbitMqRoutingKeys.StockReservationFailedV1,
                    inventoryOutbox.RoutingKeys);

                OutboxSnapshot paymentsOutbox =
                    await LoadPaymentsOutboxAsync(
                        cancellationToken);

                Assert.Equal(
                    1,
                    paymentsOutbox.Count);

                Assert.Equal(
                    1,
                    paymentsOutbox.PublishedCount);

                string paymentRoutingKey =
                    Assert.Single(
                        paymentsOutbox.RoutingKeys);

                Assert.Equal(
                    RabbitMqRoutingKeys.PaymentFailedV1,
                    paymentRoutingKey);
            },
            "All payment-failure compensation outbox messages should be published.",
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
                        .ThenBy(message =>
                            message.Id)
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
                        .ThenBy(message =>
                            message.Id)
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

    private Task<OutboxSnapshot> LoadPaymentsOutboxAsync(
        CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            PaymentsDbContext,
            OutboxSnapshot>(
            Fixture.PaymentsFactory.Services,
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
                                    == PaymentsOutboxStatus.Published,
                            token);

                string[] routingKeys =
                    await dbContext.OutboxMessages
                        .AsNoTracking()
                        .OrderBy(message =>
                            message.OccurredAtUtc)
                        .ThenBy(message =>
                            message.Id)
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

    private sealed record PaymentSnapshot(
        PaymentStatus Status,
        decimal Amount,
        string Currency,
        string PaymentMethod,
        string? FailureReason,
        DateTimeOffset? ProcessedAtUtc);

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
