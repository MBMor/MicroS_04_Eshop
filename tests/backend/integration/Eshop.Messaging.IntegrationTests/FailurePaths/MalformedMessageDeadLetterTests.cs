using System.Text;
using Eshop.Messaging.IntegrationTests.Infrastructure;
using Messaging.Shared.RabbitMq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using OrdersService.Data;
using Xunit;

namespace Eshop.Messaging.IntegrationTests.FailurePaths;

[Collection(MessagingTestCollections.System)]
public sealed class MalformedMessageDeadLetterTests(
    MessagingSystemFixture fixture)
    : MessagingIntegrationTestBase(fixture)
{
    private static readonly TimeSpan ScenarioTimeout =
        TimeSpan.FromSeconds(15);

    [Fact]
    public async Task StockReservedConsumer_InvalidJson_DeadLettersMessageWithoutSideEffects()
    {
        const string queueName =
            RabbitMqQueues.OrdersStockReservedV1;

    string deadLetterQueueName =
        RabbitMqQueues.DeadLetter(
            queueName);

    string messageId =
        Guid.NewGuid().ToString("D");

    byte[] malformedBody =
        Encoding.UTF8.GetBytes(
            """
                {
                    "payload":
                """);

    RabbitMqTestClient rabbitMqClient =
        new(Fixture);

    RabbitMqTestAdmin rabbitMqAdmin =
        new(Fixture);

    await rabbitMqClient.PublishRawToQueueAsync(
        queueName: queueName,
        body: malformedBody,
        messageId: messageId,
        messageType: "StockReservedV1");

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
            "Invalid StockReserved message should move " +
            "from the main queue to its dead-letter queue.",
            timeout: ScenarioTimeout);

        RabbitMqTestMessage? deadLetterMessage =
            await rabbitMqClient.GetAndAckAsync(
                deadLetterQueueName);

    RabbitMqTestMessage message =
        Assert.IsType<RabbitMqTestMessage>(
            deadLetterMessage);

    Assert.Equal(
        RabbitMqExchanges.DeadLetter,
        message.Exchange);

        Assert.Equal(
            deadLetterQueueName,
            message.RoutingKey);

        Assert.Equal(
            messageId,
            message.MessageId);

        Assert.Equal(
            "application/json",
            message.ContentType);

        Assert.Equal(
            "StockReservedV1",
            message.MessageType);

        Assert.Equal(
            malformedBody,
            message.Body);

        Assert.Contains(
            "x-death",
            message.Headers.Keys);

        await AssertOrdersDatabaseWasNotChangedAsync();

    Assert.Equal(
            0u,
            await rabbitMqAdmin.GetReadyMessageCountAsync(
                queueName));

        Assert.Equal(
            0u,
            await rabbitMqAdmin.GetReadyMessageCountAsync(
                deadLetterQueueName));
    }

    private Task AssertOrdersDatabaseWasNotChangedAsync()
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

                int outboxCount =
                    await dbContext.OutboxMessages
                        .CountAsync(
                            cancellationToken);

                int processedMessageCount =
                    await dbContext.ProcessedMessages
                        .CountAsync(
                            cancellationToken);

                Assert.Equal(
                    0,
                    orderCount);

                Assert.Equal(
                    0,
                    outboxCount);

                Assert.Equal(
                    0,
                    processedMessageCount);
            });
    }
}
