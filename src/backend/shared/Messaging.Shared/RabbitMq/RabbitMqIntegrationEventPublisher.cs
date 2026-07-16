using System.Text;
using Eshop.Contracts.IntegrationEvents;
using Messaging.Shared.Abstractions;
using Messaging.Shared.Contracts;
using Messaging.Shared.Serialization;
using RabbitMQ.Client;

namespace Messaging.Shared.RabbitMq;

public sealed class RabbitMqIntegrationEventPublisher(
    IRabbitMqConnectionProvider connectionProvider,
    IMessageSerializer serializer)
    : IIntegrationEventPublisher
{
    public async Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        string routingKey,
        MessagePublishContext publishContext,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        IConnection connection = await connectionProvider.GetConnectionAsync(cancellationToken);

        CreateChannelOptions channelOptions = new(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        await using IChannel channel = await connection.CreateChannelAsync(
            channelOptions,
            cancellationToken);

        MessageEnvelope<TEvent> envelope = new(
            integrationEvent.EventId,
            routingKey,
            integrationEvent.OccurredAtUtc,
            integrationEvent.CorrelationId,
            publishContext.TraceParent,
            publishContext.TraceState,
            integrationEvent);

        byte[] body = serializer.Serialize(envelope);

        BasicProperties properties = new()
        {
            Persistent = true,
            ContentType = "application/json",
            ContentEncoding = Encoding.UTF8.WebName,
            MessageId = integrationEvent.EventId.ToString(),
            CorrelationId = integrationEvent.CorrelationId.ToString(),
            Type = routingKey,
            Timestamp = new AmqpTimestamp(integrationEvent.OccurredAtUtc.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                [RabbitMqHeaders.MessageId] = integrationEvent.EventId.ToString(),
                [RabbitMqHeaders.MessageType] = routingKey,
                [RabbitMqHeaders.CorrelationId] = integrationEvent.CorrelationId.ToString(),
                [RabbitMqHeaders.OccurredAtUtc] = integrationEvent.OccurredAtUtc.ToString("O"),
                [RabbitMqHeaders.TraceParent] = publishContext.TraceParent,
                [RabbitMqHeaders.TraceState] = publishContext.TraceState
            }
        };

        await channel.BasicPublishAsync(
            exchange: RabbitMqExchanges.Events,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}
