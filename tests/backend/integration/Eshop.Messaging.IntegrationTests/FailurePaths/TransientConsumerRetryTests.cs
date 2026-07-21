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
public sealed class TransientConsumerRetryTests(
    MessagingSystemFixture fixture)
    : MessagingIntegrationTestBase(fixture)
{
    private const string CustomerId =
        "transient-retry-customer";

    private const string FailureReason =
        "One or more order items could not be reserved.";

    private const string ItemFailureReason =
        "Insufficient available stock.";

    private static readonly TimeSpan ScenarioTimeout =
        TimeSpan.FromSeconds(15);

    [Fact]
    public async Task StockReservationFailedConsumer_TransientFailure_RequeuesAndProcessesMessage()
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

        Fixture.OrdersFactory
            .TransientConsumerFailures
            .FailNext(
                eventId,
                failureCount: 1);

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
            serializer.Serialize(
                envelope);

        RabbitMqTestClient rabbitMqClient =
            new(Fixture);

        RabbitMqTestAdmin rabbitMqAdmin =
            new(Fixture);

        await rabbitMqClient.PublishRawToQueueAsync(
            queueName:
                RabbitMqQueues
                    .OrdersStockReservationFailedV1,
            body: messageBody,
            messageId:
                eventId.ToString("D"),
            messageType:
                nameof(StockReservationFailedV1));

        await Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                ProcessingSnapshot snapshot =
                    await LoadSnapshotAsync(
                        orderId,
                        eventId,
                        cancellationToken);

                Assert.Equal(
                    OrderStatus.StockReservationFailed,
                    snapshot.OrderStatus);

                Assert.Equal(
                    1,
                    snapshot.ProcessedMessageCount);

                Assert.Equal(
                    0,
                    snapshot.OutboxMessageCount);

                Assert.Equal(
                    2,
                    snapshot.StatusHistory.Length);

                Assert.Equal(
                    2,
                    Fixture.OrdersFactory
                        .TransientConsumerFailures
                        .GetAttemptCount(eventId));
            },
            $"Event '{eventId}' should fail once and " +
            "then be processed successfully.",
            timeout: ScenarioTimeout);

        await Eventually.UntilAsync(
            async cancellationToken =>
            {
                uint mainQueueCount =
                    await rabbitMqAdmin
                        .GetReadyMessageCountAsync(
                            RabbitMqQueues
                                .OrdersStockReservationFailedV1,
                            cancellationToken);

                string deadLetterQueueName =
                    RabbitMqQueues.DeadLetter(
                        RabbitMqQueues
                            .OrdersStockReservationFailedV1);

                uint deadLetterQueueCount =
                    await rabbitMqAdmin
                        .GetReadyMessageCountAsync(
                            deadLetterQueueName,
                            cancellationToken);

                return mainQueueCount == 0
                    && deadLetterQueueCount == 0;
            },
            "Successfully retried message should leave " +
            "neither the main queue nor the DLQ.",
            timeout: ScenarioTimeout);

        ProcessingSnapshot finalSnapshot =
            await LoadSnapshotAsync(
                orderId,
                eventId,
                CancellationToken.None);

        AssertProcessedExactlyOnce(
            finalSnapshot);
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
                            "Transient Retry Test Product",
                        unitPrice: 49.90m,
                        currency: "CZK",
                        quantity: 2);

                Order order =
                    Order.Create(
                        id: orderId,
                        customerId: CustomerId,
                        customerEmail:
                            "transient-retry@example.test",
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

    private Task<ProcessingSnapshot> LoadSnapshotAsync(
        Guid orderId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            OrdersDbContext,
            ProcessingSnapshot>(
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

                return new ProcessingSnapshot(
                    orderStatus,
                    statusHistory,
                    processedMessageCount,
                    outboxMessageCount);
            },
            cancellationToken);
    }

    private static void AssertProcessedExactlyOnce(
        ProcessingSnapshot snapshot)
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

    private sealed record ProcessingSnapshot(
        OrderStatus OrderStatus,
        StatusHistorySnapshot[] StatusHistory,
        int ProcessedMessageCount,
        int OutboxMessageCount);

    private sealed record StatusHistorySnapshot(
        OrderStatus? FromStatus,
        OrderStatus ToStatus,
        string Reason);
}
