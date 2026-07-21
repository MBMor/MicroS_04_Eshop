using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Eshop.Contracts.IntegrationEvents;
using Messaging.Shared.Abstractions;
using Messaging.Shared.Contracts;
using Messaging.Shared.Serialization;
using Messaging.Shared.Telemetry;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Messaging.Shared.RabbitMq;

public sealed class RabbitMqIntegrationEventPublisher(
    IRabbitMqConnectionProvider connectionProvider,
    IMessageSerializer serializer,
    IOptions<RabbitMqOptions> options)
    : IIntegrationEventPublisher
{
    private readonly RabbitMqOptions _options = options.Value;
    public async Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        string routingKey,
        MessagePublishContext publishContext,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(publishContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        using CancellationTokenSource publishTimeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);

        publishTimeout.CancelAfter(
            TimeSpan.FromSeconds(
                _options.PublishTimeoutSeconds));

        CancellationToken publishToken =
            publishTimeout.Token;

        long startedTimestamp = Stopwatch.GetTimestamp();

        using Activity? activity =
            MessagingActivity.StartPublish(
                exchange: RabbitMqExchanges.Events,
                routingKey: routingKey,
                eventType: typeof(TEvent).Name,
                eventId: integrationEvent.EventId,
                context: publishContext);

        TagList metricTags = new()
        {
            {
                "messaging.destination.name",
                RabbitMqExchanges.Events
            },
            {
                "messaging.rabbitmq.routing_key",
                routingKey
            },
            {
                "messaging.message.type",
                typeof(TEvent).Name
            }
        };

        try
        {
            IConnection connection =
                await connectionProvider.GetConnectionAsync(
                    publishToken);

            CreateChannelOptions channelOptions = new(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);

            await using IChannel channel =
                await connection.CreateChannelAsync(
                    channelOptions,
                    publishToken);

            MessageEnvelope<TEvent> envelope = new(
                integrationEvent.EventId,
                routingKey,
                integrationEvent.OccurredAtUtc,
                integrationEvent.CorrelationId,
                activity?.Id ?? publishContext.TraceParent,
                activity?.TraceStateString ?? publishContext.TraceState,
                integrationEvent);

            byte[] body = serializer.Serialize(envelope);

            BasicProperties properties = new()
            {
                Persistent = true,
                ContentType = "application/json",
                ContentEncoding = Encoding.UTF8.WebName,
                MessageId = integrationEvent.EventId.ToString("D"),
                CorrelationId =
                    integrationEvent.CorrelationId.ToString("D"),
                Type = routingKey,
                Timestamp = new AmqpTimestamp(
                    integrationEvent.OccurredAtUtc
                        .ToUnixTimeSeconds())
            };

            RabbitMqTraceContext.Inject(
                properties,
                publishContext,
                activity);

            properties.Headers ??=
                new Dictionary<string, object?>(
                    StringComparer.OrdinalIgnoreCase);

            properties.Headers[RabbitMqHeaders.MessageId] =
                integrationEvent.EventId.ToString("D");

            properties.Headers[RabbitMqHeaders.MessageType] =
                routingKey;

            properties.Headers[RabbitMqHeaders.OccurredAtUtc] =
                integrationEvent.OccurredAtUtc.ToString("O");

            await channel.BasicPublishAsync(
                exchange: RabbitMqExchanges.Events,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: publishToken);

            MessagingTelemetry.PublishedMessages.Add(
                1,
                metricTags);

            MessagingTelemetry.PublishDuration.Record(
                Stopwatch
                    .GetElapsedTime(startedTimestamp)
                    .TotalMilliseconds,
                metricTags);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception exception)
        {
            MessagingActivity.RecordFailure(
                activity,
                exception);

            TagList failureTags = new()
            {
                {
                    "messaging.operation.type",
                    "publish"
                },
                {
                    "messaging.destination.name",
                    RabbitMqExchanges.Events
                },
                {
                    "messaging.rabbitmq.routing_key",
                    routingKey
                },
                {
                    "messaging.message.type",
                    typeof(TEvent).Name
                },
                {
                    "error.type",
                    exception.GetType().Name
                }
            };

            MessagingTelemetry.FailedMessages.Add(
                1,
                failureTags);

            MessagingTelemetry.PublishDuration.Record(
                Stopwatch
                    .GetElapsedTime(startedTimestamp)
                    .TotalMilliseconds,
                failureTags);

            throw;
        }
    }
}
