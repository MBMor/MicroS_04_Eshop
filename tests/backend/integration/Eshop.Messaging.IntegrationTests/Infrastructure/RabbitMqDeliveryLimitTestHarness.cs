using Messaging.Shared.RabbitMq;
using RabbitMQ.Client;

namespace Eshop.Messaging.IntegrationTests.Infrastructure;

public sealed class RabbitMqDeliveryLimitTestHarness(
    MessagingSystemFixture fixture)
{
    public async Task DeclareAsync(
        string queueName,
        string deadLetterQueueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            queueName);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            deadLetterQueueName);

        ConnectionFactory connectionFactory =
            RabbitMqTestTopology.CreateConnectionFactory(
                fixture);

        await using IConnection connection =
            await connectionFactory.CreateConnectionAsync(
                "messaging-integration-delivery-limit-setup",
                cancellationToken);

        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        Dictionary<string, object?> mainQueueArguments =
            new(StringComparer.Ordinal)
            {
                ["x-queue-type"] =
                    RabbitMqTopologySettings.QueueType,

                ["x-delivery-limit"] =
                    RabbitMqTopologySettings.DeliveryLimit,

                ["x-dead-letter-exchange"] =
                    RabbitMqExchanges.DeadLetter,

                ["x-dead-letter-routing-key"] =
                    deadLetterQueueName
            };

        Dictionary<string, object?>
            deadLetterQueueArguments =
                new(StringComparer.Ordinal)
                {
                    ["x-queue-type"] =
                        RabbitMqTopologySettings.QueueType
                };

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArguments,
            noWait: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: deadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: deadLetterQueueArguments,
            noWait: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: deadLetterQueueName,
            exchange: RabbitMqExchanges.DeadLetter,
            routingKey: deadLetterQueueName,
            arguments: null,
            noWait: false,
            cancellationToken: cancellationToken);
    }

    public async Task<bool> TryAcquireAndAbortAsync(
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
                "messaging-integration-delivery-limit-consumer",
                cancellationToken);

        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        BasicGetResult? delivery =
            await channel.BasicGetAsync(
                queue: queueName,
                autoAck: false,
                cancellationToken: cancellationToken);

        if (delivery is null)
        {
            return false;
        }

        // Zprávu záměrně nepotvrdíme.
        // Abort channelu simuluje pád consumeru
        // během zpracování unacknowledged delivery.
        await channel.AbortAsync(
            cancellationToken);

        return true;
    }

    public async Task DeleteAsync(
        string queueName,
        string deadLetterQueueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            queueName);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            deadLetterQueueName);

        ConnectionFactory connectionFactory =
            RabbitMqTestTopology.CreateConnectionFactory(
                fixture);

        await using IConnection connection =
            await connectionFactory.CreateConnectionAsync(
                "messaging-integration-delivery-limit-cleanup",
                cancellationToken);

        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        await channel.QueueDeleteAsync(
            queue: queueName,
            ifUnused: false,
            ifEmpty: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeleteAsync(
            queue: deadLetterQueueName,
            ifUnused: false,
            ifEmpty: false,
            cancellationToken: cancellationToken);
    }
}
