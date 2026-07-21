using Eshop.Contracts.IntegrationEvents.V1;
using Eshop.Messaging.IntegrationTests.Infrastructure;
using Messaging.Shared.Contracts;
using Messaging.Shared.RabbitMq;
using Messaging.Shared.Serialization;
using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Domain;
using OrdersService.Inbox;
using Xunit;

namespace Eshop.Messaging.IntegrationTests.FailurePaths;

[Collection(MessagingTestCollections.System)]
public sealed class DuplicateMessageDeliveryTests(
    MessagingSystemFixture fixture)
    : MessagingIntegrationTestBase(fixture)
{
    private const string CustomerId =
        "duplicate-delivery-customer";

    private const string FailureReason =
        "One or more order items could not be reserved.";

    private const string ItemFailureReason =
        "Insufficient available stock.";

    private static readonly TimeSpan ScenarioTimeout =
        TimeSpan.FromSeconds(15);

    private static readonly TimeSpan DuplicateStabilityInterval =
        TimeSpan.FromSeconds(2);

    [Fact]
    public async Task StockReservationFailedConsumer_DuplicateDelivery_AppliesSideEffectsOnce()
    {
        Guid orderId =
            Guid.NewGuid();

        Guid productId =
            Guid.NewGuid();

        Guid eventId =
            Guid.NewGuid();

        Guid correlationId =
            Guid.NewGuid();

        DateTimeOffset occurredAtUtc =
            DateTimeOffset.UtcNow;

        await SeedOrderAsync(
            orderId,
            productId,
            occurredAtUtc);

        StockReservationFailedV1 integrationEvent =
            new(
                EventId: eventId,
                OccurredAtUtc: occurredAtUtc,
                CorrelationId: correlationId,
                OrderId: orderId,
                CustomerId: CustomerId,
                Reason: FailureReason,
                FailedItems:
                [
                    new StockReservationFailureItemV1(
                        ProductId: productId,
                        RequestedQuantity: 2,
                        AvailableQuantity: 0,
                        Reason: ItemFailureReason)
                ]);

        MessageEnvelope<StockReservationFailedV1> envelope =
            new(
                MessageId: eventId,
                MessageType:
                    nameof(StockReservationFailedV1),
                OccurredAtUtc: occurredAtUtc,
                CorrelationId: correlationId,
                TraceParent: null,
                TraceState: null,
                Payload: integrationEvent);

        IMessageSerializer serializer =
            new SystemTextJsonMessageSerializer();

        byte[] messageBody =
            serializer.Serialize(envelope);

        RabbitMqTestClient rabbitMqClient =
            new(Fixture);

        RabbitMqTestAdmin rabbitMqAdmin =
            new(Fixture);

        await PublishAsync(
            rabbitMqClient,
            messageBody,
            eventId);

        await Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                OrderProcessingSnapshot snapshot =
                    await LoadSnapshotAsync(
                        orderId,
                        eventId,
                        cancellationToken);

                AssertProcessedExactlyOnce(
                    snapshot);

                uint queueCount =
                    await rabbitMqAdmin
                        .GetReadyMessageCountAsync(
                            RabbitMqQueues
                                .OrdersStockReservationFailedV1,
                            cancellationToken);

                Assert.Equal(
                    0u,
                    queueCount);
            },
            $"StockReservationFailed event '{eventId}' " +
            "should be processed for the first time.",
            timeout: ScenarioTimeout);

        OrderProcessingSnapshot snapshotAfterFirstDelivery =
            await LoadSnapshotAsync(
                orderId,
                eventId,
                CancellationToken.None);

        // We publish the exact same envelope:
        // the same MessageId and the same Payload.EventId.
        await PublishAsync(
            rabbitMqClient,
            messageBody,
            eventId);

        await Eventually.UntilAsync(
            async cancellationToken =>
            {
                uint queueCount =
                    await rabbitMqAdmin
                        .GetReadyMessageCountAsync(
                            RabbitMqQueues
                                .OrdersStockReservationFailedV1,
                            cancellationToken);

                return queueCount == 0;
            },
            "Duplicate message should leave the main queue.",
            timeout: ScenarioTimeout);

        // MessageCount does not include the currently processed
        // unacknowledged message. A short stabilization interval
        // allows the consumer to complete the ACK for the second delivery.
        await Task.Delay(
            DuplicateStabilityInterval);

        OrderProcessingSnapshot snapshotAfterDuplicate =
            await LoadSnapshotAsync(
                orderId,
                eventId,
                CancellationToken.None);

        AssertProcessedExactlyOnce(
            snapshotAfterDuplicate);

        AssertSnapshotsEqual(
            snapshotAfterFirstDelivery,
            snapshotAfterDuplicate);

        string deadLetterQueueName =
            RabbitMqQueues.DeadLetter(
                RabbitMqQueues
                    .OrdersStockReservationFailedV1);

        Assert.Equal(
            0u,
            await rabbitMqAdmin.GetReadyMessageCountAsync(
                RabbitMqQueues
                    .OrdersStockReservationFailedV1));

        Assert.Equal(
            0u,
            await rabbitMqAdmin.GetReadyMessageCountAsync(
                deadLetterQueueName));
    }

    private Task SeedOrderAsync(
        Guid orderId,
        Guid productId,
        DateTimeOffset createdAtUtc)
    {
        return DatabaseTestScope.ExecuteAsync<
            OrdersDbContext>(
            Fixture.OrdersFactory.Services,
            async (dbContext, cancellationToken) =>
            {
                OrderItem orderItem =
                    OrderItem.Create(
                        id: Guid.NewGuid(),
                        orderId: orderId,
                        productId: productId,
                        productName:
                            "Duplicate Delivery Test Product",
                        unitPrice: 49.90m,
                        currency: "CZK",
                        quantity: 2);

                Order order =
                    Order.Create(
                        id: orderId,
                        customerId: CustomerId,
                        customerEmail:
                            "duplicate-delivery@example.test",
                        paymentMethod:
                            "test-success",
                        items:
                        [
                            orderItem
                        ],
                        createdAtUtc: createdAtUtc);

                dbContext.Orders.Add(
                    order);

                await dbContext.SaveChangesAsync(
                    cancellationToken);
            });
    }

    private static Task PublishAsync(
        RabbitMqTestClient rabbitMqClient,
        byte[] messageBody,
        Guid eventId)
    {
        return rabbitMqClient.PublishRawToQueueAsync(
            queueName:
                RabbitMqQueues
                    .OrdersStockReservationFailedV1,
            body: messageBody,
            messageId:
                eventId.ToString("D"),
            messageType:
                nameof(StockReservationFailedV1));
    }

    private Task<OrderProcessingSnapshot> LoadSnapshotAsync(
        Guid orderId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            OrdersDbContext,
            OrderProcessingSnapshot>(
            Fixture.OrdersFactory.Services,
            async (dbContext, token) =>
            {
                OrderStatus orderStatus =
                    await dbContext.Orders
                        .AsNoTracking()
                        .Where(order =>
                            order.Id == orderId)
                        .Select(order =>
                            order.Status)
                        .SingleAsync(token);

                StatusHistorySnapshot[] statusHistory =
                    await dbContext.OrderStatusHistories
                        .AsNoTracking()
                        .Where(history =>
                            history.OrderId == orderId)
                        .OrderBy(history =>
                            history.ChangedAtUtc)
                        .ThenBy(history =>
                            history.Id)
                        .Select(history =>
                            new StatusHistorySnapshot(
                                history.FromStatus,
                                history.ToStatus,
                                history.Reason))
                        .ToArrayAsync(token);

                int processedMessageCount =
                    await dbContext.ProcessedMessages
                        .CountAsync(
                            message =>
                                message.EventId == eventId
                                && message.ConsumerName
                                    == ConsumerNames
                                        .StockReservationFailed,
                            token);

                int outboxMessageCount =
                    await dbContext.OutboxMessages
                        .CountAsync(token);

                return new OrderProcessingSnapshot(
                    orderStatus,
                    statusHistory,
                    processedMessageCount,
                    outboxMessageCount);
            },
            cancellationToken);
    }

    private static void AssertProcessedExactlyOnce(
        OrderProcessingSnapshot snapshot)
    {
        Assert.Equal(
            OrderStatus.StockReservationFailed,
            snapshot.OrderStatus);

        Assert.Equal(
            1,
            snapshot.ProcessedMessageCount);

        Assert.Equal(
            0,
            snapshot.OutboxMessageCount);

        Assert.Collection(
            snapshot.StatusHistory,
            created =>
            {
                Assert.Null(
                    created.FromStatus);

                Assert.Equal(
                    OrderStatus.PendingStockReservation,
                    created.ToStatus);

                Assert.Equal(
                    "Order created and awaiting stock reservation.",
                    created.Reason);
            },
            failed =>
            {
                Assert.Equal(
                    OrderStatus.PendingStockReservation,
                    failed.FromStatus);

                Assert.Equal(
                    OrderStatus.StockReservationFailed,
                    failed.ToStatus);

                Assert.Equal(
                    FailureReason,
                    failed.Reason);
            });
    }

    private sealed record OrderProcessingSnapshot(
        OrderStatus OrderStatus,
        StatusHistorySnapshot[] StatusHistory,
        int ProcessedMessageCount,
        int OutboxMessageCount);

    private sealed record StatusHistorySnapshot(
        OrderStatus? FromStatus,
        OrderStatus ToStatus,
        string Reason);

    private static void AssertSnapshotsEqual(
    OrderProcessingSnapshot expected,
    OrderProcessingSnapshot actual)
    {
        Assert.Equal(
            expected.OrderStatus,
            actual.OrderStatus);

        Assert.Equal(
            expected.ProcessedMessageCount,
            actual.ProcessedMessageCount);

        Assert.Equal(
            expected.OutboxMessageCount,
            actual.OutboxMessageCount);

        Assert.Equal(
            expected.StatusHistory.Length,
            actual.StatusHistory.Length);

        Assert.True(
            expected.StatusHistory.SequenceEqual(
                actual.StatusHistory),
            "Status history changed after duplicate delivery.");
    }
}
