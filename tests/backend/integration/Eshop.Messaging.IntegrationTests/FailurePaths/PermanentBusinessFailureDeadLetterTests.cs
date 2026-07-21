using Eshop.Contracts.IntegrationEvents.V1;
using Eshop.Messaging.IntegrationTests.Infrastructure;
using Messaging.Shared.Contracts;
using Messaging.Shared.RabbitMq;
using Messaging.Shared.Serialization;
using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using Xunit;

namespace Eshop.Messaging.IntegrationTests.FailurePaths;

[Collection(MessagingTestCollections.System)]
public sealed class PermanentBusinessFailureDeadLetterTests(
    MessagingSystemFixture fixture)
    : MessagingIntegrationTestBase(fixture)
{
    private static readonly TimeSpan ScenarioTimeout =
        TimeSpan.FromSeconds(15);

    [Fact]
    public async Task StockReservedConsumer_UnknownOrder_DeadLettersMessageWithoutRetry()
    {
        Guid eventId =
            Guid.NewGuid();

        Guid correlationId =
            Guid.NewGuid();

        Guid missingOrderId =
            Guid.NewGuid();

        DateTimeOffset occurredAtUtc =
            DateTimeOffset.UtcNow;

        StockReservedV1 integrationEvent =
            new(
                EventId: eventId,
                OccurredAtUtc: occurredAtUtc,
                CorrelationId: correlationId,
                OrderId: missingOrderId,
                CustomerId: "missing-order-customer",
                Items:
                [
                    new ReservedStockItemV1(
                        ProductId: Guid.NewGuid(),
                        Quantity: 2)
                ]);

        MessageEnvelope<StockReservedV1> envelope =
            new(
                MessageId: eventId,
                MessageType:
                    nameof(StockReservedV1),
                OccurredAtUtc: occurredAtUtc,
                CorrelationId: correlationId,
                TraceParent: null,
                TraceState: null,
                Payload: integrationEvent);

        IMessageSerializer serializer =
            new SystemTextJsonMessageSerializer();

        byte[] messageBody =
            serializer.Serialize(envelope);

        const string queueName =
            RabbitMqQueues.OrdersStockReservedV1;

        string deadLetterQueueName =
            RabbitMqQueues.DeadLetter(
                queueName);

        RabbitMqTestClient rabbitMqClient =
            new(Fixture);

        RabbitMqTestAdmin rabbitMqAdmin =
            new(Fixture);

        await rabbitMqClient.PublishRawToQueueAsync(
            queueName: queueName,
            body: messageBody,
            messageId: eventId.ToString("D"),
            messageType: nameof(StockReservedV1));

        await Eventually.UntilAsync(
            async cancellationToken =>
            {
                uint mainQueueCount =
                    await rabbitMqAdmin
                        .GetReadyMessageCountAsync(
                            queueName,
                            cancellationToken);

                uint deadLetterQueueCount =
                    await rabbitMqAdmin
                        .GetReadyMessageCountAsync(
                            deadLetterQueueName,
                            cancellationToken);

                return mainQueueCount == 0
                    && deadLetterQueueCount == 1;
            },
            $"StockReserved event '{eventId}' for missing " +
            $"order '{missingOrderId}' should be dead-lettered.",
            timeout: ScenarioTimeout);

        RabbitMqTestMessage? receivedMessage =
            await rabbitMqClient.GetAndAckAsync(
                deadLetterQueueName);

        RabbitMqTestMessage deadLetterMessage =
            Assert.IsType<RabbitMqTestMessage>(
                receivedMessage);

        Assert.Equal(
            RabbitMqExchanges.DeadLetter,
            deadLetterMessage.Exchange);

        Assert.Equal(
            deadLetterQueueName,
            deadLetterMessage.RoutingKey);

        Assert.Equal(
            eventId.ToString("D"),
            deadLetterMessage.MessageId);

        Assert.Equal(
            nameof(StockReservedV1),
            deadLetterMessage.MessageType);

        Assert.Equal(
            "application/json",
            deadLetterMessage.ContentType);

        Assert.Equal(
            messageBody,
            deadLetterMessage.Body);

        Assert.Contains(
            "x-death",
            deadLetterMessage.Headers.Keys);

        await AssertNoDatabaseSideEffectsAsync(
            eventId);

        Assert.Equal(
            0u,
            await rabbitMqAdmin.GetReadyMessageCountAsync(
                queueName));

        Assert.Equal(
            0u,
            await rabbitMqAdmin.GetReadyMessageCountAsync(
                deadLetterQueueName));
    }

    private Task AssertNoDatabaseSideEffectsAsync(
        Guid eventId)
    {
        return DatabaseTestScope.ExecuteAsync<
            OrdersDbContext>(
            Fixture.OrdersFactory.Services,
            async (dbContext, cancellationToken) =>
            {
                int orderCount =
                    await dbContext.Orders
                        .CountAsync(
                            cancellationToken);

                int statusHistoryCount =
                    await dbContext.OrderStatusHistories
                        .CountAsync(
                            cancellationToken);

                int outboxCount =
                    await dbContext.OutboxMessages
                        .CountAsync(
                            cancellationToken);

                int processedMessageCount =
                    await dbContext.ProcessedMessages
                        .CountAsync(
                            message =>
                                message.EventId == eventId,
                            cancellationToken);

                Assert.Equal(
                    0,
                    orderCount);

                Assert.Equal(
                    0,
                    statusHistoryCount);

                Assert.Equal(
                    0,
                    outboxCount);

                Assert.Equal(
                    0,
                    processedMessageCount);
            });
    }
}
