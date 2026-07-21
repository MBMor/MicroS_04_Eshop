using RabbitMQ.Client;

namespace Eshop.Messaging.IntegrationTests.Infrastructure;

public sealed class RabbitMqTestClient(
    MessagingSystemFixture fixture)
{
    public async Task PublishRawToQueueAsync(
        string queueName,
        ReadOnlyMemory<byte> body,
        string messageId,
        string messageType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            queueName);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            messageId);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            messageType);

        ConnectionFactory connectionFactory =
            RabbitMqTestTopology.CreateConnectionFactory(
                fixture);

        await using IConnection connection =
            await connectionFactory.CreateConnectionAsync(
                "messaging-integration-test-publisher",
                cancellationToken);

        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        BasicProperties properties = new()
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = messageId,
            CorrelationId = Guid.NewGuid().ToString("D"),
            Type = messageType,
            Timestamp = new AmqpTimestamp(
                DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    public async Task<RabbitMqTestMessage?> GetAndAckAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            queueName);

        ConnectionFactory connectionFactory =
            RabbitMqTestTopology.CreateConnectionFactory(
                fixture);

        await using IConnection connection =
            await connectionFactory.CreateConnectionAsync(
                "messaging-integration-test-reader",
                cancellationToken);

        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        BasicGetResult? result =
            await channel.BasicGetAsync(
                queue: queueName,
                autoAck: false,
                cancellationToken: cancellationToken);

        if (result is null)
        {
            return null;
        }

        RabbitMqTestMessage message =
            CreateMessage(result);

        await channel.BasicAckAsync(
            deliveryTag: result.DeliveryTag,
            multiple: false,
            cancellationToken: cancellationToken);

        return message;
    }

    private static RabbitMqTestMessage CreateMessage(
        BasicGetResult result)
    {
        IReadOnlyDictionary<string, object?> headers =
            result.BasicProperties.Headers is null
                ? new Dictionary<string, object?>(
                    StringComparer.Ordinal)
                : new Dictionary<string, object?>(
                    result.BasicProperties.Headers,
                    StringComparer.Ordinal);

        return new RabbitMqTestMessage(
            Exchange: result.Exchange,
            RoutingKey: result.RoutingKey,
            MessageId:
                result.BasicProperties.MessageId,
            CorrelationId:
                result.BasicProperties.CorrelationId,
            ContentType:
                result.BasicProperties.ContentType,
            MessageType:
                result.BasicProperties.Type,
            Headers: headers,
            Body: result.Body.ToArray());
    }
}

public sealed record RabbitMqTestMessage(
    string Exchange,
    string RoutingKey,
    string? MessageId,
    string? CorrelationId,
    string? ContentType,
    string? MessageType,
    IReadOnlyDictionary<string, object?> Headers,
    byte[] Body);
