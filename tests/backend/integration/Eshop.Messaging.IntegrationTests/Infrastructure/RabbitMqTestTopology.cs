using Messaging.Shared.RabbitMq;
using RabbitMQ.Client;

namespace Eshop.Messaging.IntegrationTests.Infrastructure;

public static class RabbitMqTestTopology
{
    public static async Task DeclareAsync(
        MessagingSystemFixture fixture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        ConnectionFactory connectionFactory =
            CreateConnectionFactory(fixture);

        await using IConnection connection =
            await connectionFactory.CreateConnectionAsync(
                "messaging-integration-test-topology",
                cancellationToken);

        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        await DeclareExchangesAsync(
            channel,
            cancellationToken);

        foreach (
            RabbitMqBindingDefinition binding
            in RabbitMqTopology.Bindings)
        {
            await DeclareBindingAsync(
                channel,
                binding,
                cancellationToken);
        }
    }

    private static async Task DeclareExchangesAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            exchange: RabbitMqExchanges.Events,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            noWait: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: RabbitMqExchanges.DeadLetter,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            noWait: false,
            cancellationToken: cancellationToken);
    }

    private static async Task DeclareBindingAsync(
        IChannel channel,
        RabbitMqBindingDefinition binding,
        CancellationToken cancellationToken)
    {
        string deadLetterQueueName =
            RabbitMqQueues.DeadLetter(
                binding.QueueName);

        Dictionary<string, object?> queueArguments =
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

        Dictionary<string, object?> deadLetterQueueArguments =
            new(StringComparer.Ordinal)
            {
                ["x-queue-type"] =
                    RabbitMqTopologySettings.QueueType
            };

        await channel.QueueDeclareAsync(
            queue: binding.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
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
            queue: binding.QueueName,
            exchange: RabbitMqExchanges.Events,
            routingKey: binding.RoutingKey,
            arguments: null,
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

    internal static ConnectionFactory CreateConnectionFactory(
        MessagingSystemFixture fixture)
    {
        return new ConnectionFactory
        {
            HostName = fixture.RabbitMqHostName,
            Port = fixture.RabbitMqPort,
            UserName =
                MessagingSystemFixture.RabbitMqUserName,
            Password =
                MessagingSystemFixture.RabbitMqPassword,
            VirtualHost =
                MessagingSystemFixture.RabbitMqVirtualHost,
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false,
            RequestedHeartbeat =
                TimeSpan.FromSeconds(10)
        };
    }
}
