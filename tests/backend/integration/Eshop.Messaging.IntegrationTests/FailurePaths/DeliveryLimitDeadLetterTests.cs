using System.Text;
using Eshop.Messaging.IntegrationTests.Infrastructure;
using Messaging.Shared.RabbitMq;
using Xunit;

namespace Eshop.Messaging.IntegrationTests.FailurePaths;

[Collection(MessagingTestCollections.System)]
public sealed class DeliveryLimitDeadLetterTests(
    MessagingSystemFixture fixture)
    : MessagingIntegrationTestBase(fixture)
{
    private static readonly TimeSpan ScenarioTimeout =
        TimeSpan.FromSeconds(15);

    private const int MaximumFailureAttempts =
        RabbitMqTopologySettings.DeliveryLimit + 3;

    [Fact]
    public async Task QuorumQueue_DeliveryLimitExceeded_DeadLettersMessage()
    {
        string testId =
            Guid.NewGuid().ToString("N");

        string queueName =
            $"tests.delivery-limit.{testId}";

        string deadLetterQueueName =
            $"{queueName}.dlq";

        string messageId =
            Guid.NewGuid().ToString("D");

        byte[] messageBody =
            Encoding.UTF8.GetBytes(
                """
                {
                  "message": "delivery-limit-test"
                }
                """);

        RabbitMqDeliveryLimitTestHarness harness =
            new(fixture);

        RabbitMqTestClient rabbitMqClient =
            new(fixture);

        RabbitMqTestAdmin rabbitMqAdmin =
            new(fixture);

        await harness.DeclareAsync(
            queueName,
            deadLetterQueueName);

        try
        {
            await rabbitMqClient.PublishRawToQueueAsync(
                queueName: queueName,
                body: messageBody,
                messageId: messageId,
                messageType: "DeliveryLimitProbe");

            await Eventually.UntilAsync(
                async cancellationToken =>
                {
                    uint messageCount =
                        await rabbitMqAdmin
                            .GetReadyMessageCountAsync(
                                queueName,
                                cancellationToken);

                    return messageCount == 1;
                },
                "Delivery-limit probe should be available " +
                "in the source queue.",
                timeout: ScenarioTimeout);

            int failedDeliveryCount = 0;

            for (
                int attempt = 1;
                attempt <= MaximumFailureAttempts;
                attempt++)
            {
                uint deadLetterCount =
                    await rabbitMqAdmin
                        .GetReadyMessageCountAsync(
                            deadLetterQueueName);

                if (deadLetterCount == 1)
                {
                    break;
                }

                await Eventually.UntilAsync(
                    async cancellationToken =>
                    {
                        return await harness
                            .TryAcquireAndAbortAsync(
                                queueName,
                                cancellationToken);
                    },
                    $"Delivery attempt {attempt} should acquire " +
                    "the probe message.",
                    timeout: ScenarioTimeout);

                failedDeliveryCount++;

                await Eventually.UntilAsync(
                    async cancellationToken =>
                    {
                        uint mainQueueCount =
                            await rabbitMqAdmin
                                .GetReadyMessageCountAsync(
                                    queueName,
                                    cancellationToken);

                        uint dlqCount =
                            await rabbitMqAdmin
                                .GetReadyMessageCountAsync(
                                    deadLetterQueueName,
                                    cancellationToken);

                        return mainQueueCount == 1
                            || dlqCount == 1;
                    },
                    "After an aborted delivery, the message " +
                    "should be requeued or dead-lettered.",
                    timeout: ScenarioTimeout);
            }

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
                "Message should be dead-lettered after " +
                "exceeding the quorum queue delivery limit.",
                timeout: ScenarioTimeout);

            Assert.True(
                failedDeliveryCount
                    >= RabbitMqTopologySettings.DeliveryLimit,
                $"Expected at least " +
                $"{RabbitMqTopologySettings.DeliveryLimit} " +
                $"failed deliveries, but observed " +
                $"{failedDeliveryCount}.");

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
                messageId,
                deadLetterMessage.MessageId);

            Assert.Equal(
                "DeliveryLimitProbe",
                deadLetterMessage.MessageType);

            Assert.Equal(
                messageBody,
                deadLetterMessage.Body);

            Assert.Contains(
                "x-death",
                deadLetterMessage.Headers.Keys);

            Assert.Equal(
                "delivery_limit",
                ReadStringHeader(
                    deadLetterMessage.Headers,
                    "x-first-death-reason"));

            Assert.Equal(
                queueName,
                ReadStringHeader(
                    deadLetterMessage.Headers,
                    "x-first-death-queue"));

            Assert.Equal(
                0u,
                await rabbitMqAdmin
                    .GetReadyMessageCountAsync(
                        queueName));

            Assert.Equal(
                0u,
                await rabbitMqAdmin
                    .GetReadyMessageCountAsync(
                        deadLetterQueueName));
        }
        finally
        {
            await harness.DeleteAsync(
                queueName,
                deadLetterQueueName);
        }
    }

    private static string? ReadStringHeader(
        IReadOnlyDictionary<string, object?> headers,
        string headerName)
    {
        if (!headers.TryGetValue(
                headerName,
                out object? value))
        {
            return null;
        }

        return value switch
        {
            string text =>
                text,

            byte[] bytes =>
                Encoding.UTF8.GetString(bytes),

            ReadOnlyMemory<byte> memory =>
                Encoding.UTF8.GetString(memory.Span),

            _ =>
                value?.ToString()
        };
    }
}
