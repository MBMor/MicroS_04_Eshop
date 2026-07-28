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
public sealed class OrderHappyPathMessagingTests(
    MessagingSystemFixture fixture)
    : MessagingIntegrationTestBase(fixture)
{
    private const string CustomerId =
        "happy-path-customer";

    private const string CustomerEmail =
        "happy-path@example.test";

    private const string Currency =
        "CZK";

    private static readonly TimeSpan ScenarioTimeout =
        TimeSpan.FromSeconds(30);

    [Fact]
    public async Task CreateOrderHappyPathConfirmsOrder()
    {
        CheckoutScenario scenario =
            await ArrangeHappyPathAsync();

        OrderResponse createdOrder =
            await CreateOrderAsync();

        AssertInitialOrderResponse(createdOrder);

        Assert.True(
            Fixture.OrdersFactory.BasketClient.WasCleared(
                CustomerId));

        await AssertCompleteWorkflowAsync(
            createdOrder,
            scenario);
    }

    [Fact]
    public Task DuplicateCheckoutReplayCreatesOneCompleteWorkflow()
    {
        return AssertDuplicateCheckoutCreatesOneCompleteWorkflowAsync(
            concurrent: false);
    }

    [Fact]
    public Task ConcurrentDuplicateCheckoutCreatesOneCompleteWorkflow()
    {
        return AssertDuplicateCheckoutCreatesOneCompleteWorkflowAsync(
            concurrent: true);
    }

    private async Task<OrderResponse> CreateOrderAsync()
    {
        CreateOrderHttpResult result =
            await SendCreateOrderAsync(
                Guid.NewGuid().ToString());

        Assert.Equal(
            HttpStatusCode.Created,
            result.StatusCode);

        Assert.False(result.IsReplay);

        return result.Order;
    }

    private async Task AssertDuplicateCheckoutCreatesOneCompleteWorkflowAsync(
        bool concurrent)
    {
        CheckoutScenario scenario =
            await ArrangeHappyPathAsync();

        string idempotencyKey =
            Guid.NewGuid().ToString();

        CreateOrderHttpResult[] results;

        if (concurrent)
        {
            Fixture.OrdersFactory.BasketClient
                .SynchronizeNextBasketReads(
                    CustomerId,
                    participantCount: 2);

            results = await Task.WhenAll(
                SendCreateOrderAsync(idempotencyKey),
                SendCreateOrderAsync(idempotencyKey));
        }
        else
        {
            results =
            [
                await SendCreateOrderAsync(idempotencyKey),
                await SendCreateOrderAsync(idempotencyKey)
            ];
        }

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.Created],
            results
                .Select(result => result.StatusCode)
                .Order()
                .ToArray());

        CreateOrderHttpResult createdResult =
            Assert.Single(
                results,
                result =>
                    result.StatusCode
                        == HttpStatusCode.Created);

        CreateOrderHttpResult replayResult =
            Assert.Single(
                results,
                result =>
                    result.StatusCode
                        == HttpStatusCode.OK);

        Assert.False(createdResult.IsReplay);
        Assert.True(replayResult.IsReplay);
        Assert.Equal(createdResult.Order.Id, replayResult.Order.Id);

        string createdLocation =
            Assert.IsType<string>(createdResult.Location);

        Assert.True(
            Uri.TryCreate(
                createdLocation,
                UriKind.Absolute,
                out _));

        Assert.Equal(
            createdLocation,
            replayResult.Location);

        Assert.Equal(
            createdResult.Order.TotalAmount,
            replayResult.Order.TotalAmount);

        Assert.Equal(
            createdResult.Order.Items,
            replayResult.Order.Items);

        AssertInitialOrderResponse(createdResult.Order);

        Assert.True(
            Fixture.OrdersFactory.BasketClient.WasCleared(
                CustomerId));

        Assert.Equal(
            concurrent ? 2 : 1,
            Fixture.OrdersFactory.BasketClient
                .GetBasketCallCount(CustomerId));

        Assert.Equal(
            1,
            Fixture.OrdersFactory.BasketClient
                .GetClearCallCount(CustomerId));

        await AssertOrderAndIdempotencyCardinalityAsync(
            createdResult.Order.Id);

        await AssertCompleteWorkflowAsync(
            createdResult.Order,
            scenario);
    }

    private async Task<CreateOrderHttpResult> SendCreateOrderAsync(
        string idempotencyKey)
    {
        using HttpClient client =
            Fixture.OrdersFactory.CreateClient();

        client.DefaultRequestHeaders.Add(
            TestOrderOwnerProvider.CustomerIdHeaderName,
            CustomerId);

        client.DefaultRequestHeaders.Add(
            OrderHeaders.IdempotencyKey,
            idempotencyKey);

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

        OrderResponse? order =
            await response.Content
                .ReadFromJsonAsync<OrderResponse>();

        bool isReplay =
            response.Headers.TryGetValues(
                OrderHeaders.IdempotentReplayed,
                out IEnumerable<string>? replayValues)
            && string.Equals(
                Assert.Single(replayValues),
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase);

        return new CreateOrderHttpResult(
            response.StatusCode,
            Assert.IsType<OrderResponse>(order),
            response.Headers.Location?.ToString(),
            isReplay);
    }

    private async Task<CheckoutScenario> ArrangeHappyPathAsync()
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
                    ProductName: "Integration Test Product",
                    UnitPrice: unitPrice,
                    Currency: Currency,
                    Quantity: orderedQuantity,
                    LineTotal:
                        unitPrice * orderedQuantity)
            ]));

        return new CheckoutScenario(
            productId,
            initialStock,
            orderedQuantity,
            unitPrice);
    }

    private static void AssertInitialOrderResponse(
        OrderResponse order)
    {
        Assert.NotEqual(Guid.Empty, order.Id);

        Assert.Equal(
            OrderStatus.PendingStockReservation.ToString(),
            order.Status);

        Assert.Single(order.Items);
        Assert.Single(order.StatusHistory);
    }

    private async Task AssertCompleteWorkflowAsync(
        OrderResponse createdOrder,
        CheckoutScenario scenario)
    {
        await Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                OrderSnapshot order =
                    await LoadOrderAsync(
                        createdOrder.Id,
                        cancellationToken);

                Assert.Equal(
                    OrderStatus.Confirmed,
                    order.Status);

                Assert.Collection(
                    order.StatusHistory,
                    history =>
                    {
                        Assert.Null(history.FromStatus);

                        Assert.Equal(
                            OrderStatus.PendingStockReservation,
                            history.ToStatus);
                    },
                    history =>
                    {
                        Assert.Equal(
                            OrderStatus.PendingStockReservation,
                            history.FromStatus);

                        Assert.Equal(
                            OrderStatus.PendingPayment,
                            history.ToStatus);
                    },
                    history =>
                    {
                        Assert.Equal(
                            OrderStatus.PendingPayment,
                            history.FromStatus);

                        Assert.Equal(
                            OrderStatus.Confirmed,
                            history.ToStatus);
                    });
            },
            $"Order '{createdOrder.Id}' should become confirmed.",
            timeout: ScenarioTimeout);

        await AssertInventoryReservationAsync(
            scenario.ProductId,
            scenario.InitialStock,
            scenario.OrderedQuantity);

        await AssertAuthorizedPaymentAsync(
            createdOrder.Id,
            scenario.UnitPrice
                * scenario.OrderedQuantity);

        await AssertNotificationsAsync(
            createdOrder.Id);

        await AssertOutboxMessagesPublishedAsync();

        await AssertInboxMessagesProcessedOnceAsync();

        await AssertMessagingQueuesAreEmptyAsync();
    }

    private Task AssertOrderAndIdempotencyCardinalityAsync(
        Guid orderId)
    {
        return DatabaseTestScope.ExecuteAsync<
            OrdersDbContext>(
            Fixture.OrdersFactory.Services,
            async (dbContext, cancellationToken) =>
            {
                Guid persistedOrderId =
                    await dbContext.Orders
                        .AsNoTracking()
                        .Select(order => order.Id)
                        .SingleAsync(cancellationToken);

                Guid idempotentOrderId =
                    await dbContext.OrderIdempotencyRecords
                        .AsNoTracking()
                        .Select(record => record.OrderId)
                        .SingleAsync(cancellationToken);

                Assert.Equal(orderId, persistedOrderId);
                Assert.Equal(orderId, idempotentOrderId);
            });
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

    private Task AssertInventoryReservationAsync(
        Guid productId,
        int initialStock,
        int orderedQuantity)
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
                    orderedQuantity,
                    inventory.ReservedQuantity);

                Assert.Equal(
                    initialStock - orderedQuantity,
                    inventory.AvailableQuantity);
            },
            $"Inventory for product '{productId}' should be reserved.",
            timeout: ScenarioTimeout);
    }

    private Task AssertAuthorizedPaymentAsync(
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
                                        payment.FailureReason))
                                .SingleAsync(token);
                        },
                        cancellationToken);

                Assert.Equal(
                    PaymentStatus.Authorized,
                    payment.Status);

                Assert.Equal(
                    expectedAmount,
                    payment.Amount);

                Assert.Equal(
                    Currency,
                    payment.Currency);

                Assert.Equal(
                    FakePaymentProcessor.SuccessMethod,
                    payment.PaymentMethod);

                Assert.Null(
                    payment.FailureReason);
            },
            $"Payment for order '{orderId}' should be authorized.",
            timeout: ScenarioTimeout);
    }

    private Task AssertNotificationsAsync(
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
                                    notification.OrderId == orderId)
                                .OrderBy(notification =>
                                    notification.CreatedAtUtc)
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
                    NotificationType.PaymentAuthorized,
                    notificationTypes);

                Assert.Contains(
                    NotificationType.OrderConfirmed,
                    notificationTypes);
            },
            $"Notifications for order '{orderId}' should be created.",
            timeout: ScenarioTimeout);
    }

    private async Task AssertOutboxMessagesPublishedAsync()
    {
        await Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                OutboxSnapshot ordersOutbox =
                    await LoadOrdersOutboxAsync(
                        cancellationToken);

                Assert.Equal(
                    3,
                    ordersOutbox.Count);

                Assert.Equal(
                    3,
                    ordersOutbox.PublishedCount);

                Assert.Contains(
                    RabbitMqRoutingKeys.OrderCreatedV1,
                    ordersOutbox.RoutingKeys);

                Assert.Contains(
                    RabbitMqRoutingKeys.PaymentRequestedV1,
                    ordersOutbox.RoutingKeys);

                Assert.Contains(
                    RabbitMqRoutingKeys.OrderConfirmedV1,
                    ordersOutbox.RoutingKeys);

                OutboxSnapshot inventoryOutbox =
                    await LoadInventoryOutboxAsync(
                        cancellationToken);

                Assert.Equal(
                    1,
                    inventoryOutbox.Count);

                Assert.Equal(
                    1,
                    inventoryOutbox.PublishedCount);

                Assert.Contains(
                    RabbitMqRoutingKeys.StockReservedV1,
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

                Assert.Contains(
                    RabbitMqRoutingKeys.PaymentAuthorizedV1,
                    paymentsOutbox.RoutingKeys);
            },
            "All happy-path outbox messages should be published.",
            timeout: ScenarioTimeout);
    }

    private Task AssertInboxMessagesProcessedOnceAsync()
    {
        return Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                Task<int> ordersCount =
                    DatabaseTestScope.ExecuteAsync<
                        OrdersDbContext,
                        int>(
                        Fixture.OrdersFactory.Services,
                        (dbContext, token) =>
                            dbContext.ProcessedMessages
                                .CountAsync(token),
                        cancellationToken);

                Task<int> inventoryCount =
                    DatabaseTestScope.ExecuteAsync<
                        InventoryDbContext,
                        int>(
                        Fixture.InventoryFactory.Services,
                        (dbContext, token) =>
                            dbContext.ProcessedMessages
                                .CountAsync(token),
                        cancellationToken);

                Task<int> paymentsCount =
                    DatabaseTestScope.ExecuteAsync<
                        PaymentsDbContext,
                        int>(
                        Fixture.PaymentsFactory.Services,
                        (dbContext, token) =>
                            dbContext.ProcessedMessages
                                .CountAsync(token),
                        cancellationToken);

                Task<int> notificationsCount =
                    DatabaseTestScope.ExecuteAsync<
                        NotificationsDbContext,
                        int>(
                        Fixture.NotificationsFactory.Services,
                        (dbContext, token) =>
                            dbContext.ProcessedMessages
                                .CountAsync(token),
                        cancellationToken);

                int[] counts = await Task.WhenAll(
                    ordersCount,
                    inventoryCount,
                    paymentsCount,
                    notificationsCount);

                Assert.Equal(
                    [2, 1, 1, 4],
                    counts);
            },
            "Each happy-path event should produce one inbox record per consumer.",
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

    private sealed record InventorySnapshot(
        int OnHandQuantity,
        int ReservedQuantity)
    {
        public int AvailableQuantity =>
            OnHandQuantity - ReservedQuantity;
    }

    private sealed record PaymentSnapshot(
        PaymentStatus Status,
        decimal Amount,
        string Currency,
        string PaymentMethod,
        string? FailureReason);

    private sealed record OutboxSnapshot(
        int Count,
        int PublishedCount,
        string[] RoutingKeys);

    private sealed record CheckoutScenario(
        Guid ProductId,
        int InitialStock,
        int OrderedQuantity,
        decimal UnitPrice);

    private sealed record CreateOrderHttpResult(
        HttpStatusCode StatusCode,
        OrderResponse Order,
        string? Location,
        bool IsReplay);
}
