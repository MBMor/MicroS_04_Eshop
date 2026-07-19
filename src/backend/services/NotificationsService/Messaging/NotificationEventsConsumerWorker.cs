using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Eshop.Contracts.IntegrationEvents;
using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.Contracts;
using Messaging.Shared.RabbitMq;
using Messaging.Shared.Serialization;
using Messaging.Shared.Telemetry;
using Microsoft.EntityFrameworkCore;
using NotificationsService.Application;
using NotificationsService.Inbox;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationsService.Messaging;

public sealed class NotificationEventsConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IRabbitMqConnectionProvider connectionProvider,
    IMessageSerializer serializer,
    ILogger<NotificationEventsConsumerWorker> logger)
    : BackgroundService
{
    private static readonly Action<ILogger, string, ulong, Exception?>
        LogInvalidJson =
            LoggerMessage.Define<string, ulong>(
                LogLevel.Error,
                new EventId(4000, nameof(LogInvalidJson)),
                "Notification message from queue {QueueName} with delivery tag {DeliveryTag} contains invalid JSON.");

    private static readonly Action<ILogger, string, ulong, Exception?>
        LogProcessingFailed =
            LoggerMessage.Define<string, ulong>(
                LogLevel.Error,
                new EventId(4001, nameof(LogProcessingFailed)),
                "Notification message from queue {QueueName} with delivery tag {DeliveryTag} failed unexpectedly and will be retried.");

    private static readonly Action<ILogger, string, ulong, Exception?>
        LogPermanentFailure =
            LoggerMessage.Define<string, ulong>(
                LogLevel.Error,
                new EventId(4002, nameof(LogPermanentFailure)),
                "Notification message from queue {QueueName} with delivery tag {DeliveryTag} failed permanently and will be dead-lettered.");

    private static readonly Action<ILogger, string, ulong, Exception?>
        LogTransientFailure =
            LoggerMessage.Define<string, ulong>(
                LogLevel.Warning,
                new EventId(4003, nameof(LogTransientFailure)),
                "Notification message from queue {QueueName} with delivery tag {DeliveryTag} failed transiently and will be retried.");

    private static readonly Action<ILogger, string, ulong, Exception?>
        LogDuplicateMessage =
            LoggerMessage.Define<string, ulong>(
                LogLevel.Information,
                new EventId(4004, nameof(LogDuplicateMessage)),
                "Notification message from queue {QueueName} with delivery tag {DeliveryTag} was already processed by another consumer instance.");

    private IChannel? _channel;
    private CancellationToken _stoppingToken;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        IConnection connection =
            await connectionProvider.GetConnectionAsync(
                stoppingToken);

        _channel = await connection.CreateChannelAsync(
            cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 16,
            global: false,
            cancellationToken: stoppingToken);

        await RegisterConsumerAsync<OrderCreatedV1>(
            RabbitMqQueues.NotificationsOrderCreatedV1,
            stoppingToken);

        await RegisterConsumerAsync<StockReservedV1>(
            RabbitMqQueues.NotificationsStockReservedV1,
            stoppingToken);

        await RegisterConsumerAsync<StockReservationFailedV1>(
            RabbitMqQueues.NotificationsStockReservationFailedV1,
            stoppingToken);

        await RegisterConsumerAsync<PaymentAuthorizedV1>(
            RabbitMqQueues.NotificationsPaymentAuthorizedV1,
            stoppingToken);

        await RegisterConsumerAsync<PaymentFailedV1>(
            RabbitMqQueues.NotificationsPaymentFailedV1,
            stoppingToken);

        await RegisterConsumerAsync<OrderConfirmedV1>(
            RabbitMqQueues.NotificationsOrderConfirmedV1,
            stoppingToken);

        await RegisterConsumerAsync<OrderCancelledV1>(
            RabbitMqQueues.NotificationsOrderCancelledV1,
            stoppingToken);

        await Task.Delay(
            Timeout.InfiniteTimeSpan,
            stoppingToken);
    }

    private async Task RegisterConsumerAsync<TEvent>(
        string queueName,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        IChannel channel =
            _channel
            ?? throw new InvalidOperationException(
                "RabbitMQ channel has not been initialized.");

        AsyncEventingBasicConsumer consumer =
            new(channel);

        consumer.ReceivedAsync += (_, delivery) =>
            HandleDeliveryAsync<TEvent>(
                queueName,
                delivery);

        await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);
    }

    private async Task HandleDeliveryAsync<TEvent>(
        string queueName,
        BasicDeliverEventArgs delivery)
        where TEvent : IIntegrationEvent
    {
        IChannel? channel = _channel;

        if (channel is null)
        {
            return;
        }

        string eventType =
            typeof(TEvent).Name;

        long startedTimestamp =
            Stopwatch.GetTimestamp();

        string outcome = "unknown";
        Exception? processingException = null;

        Dictionary<string, object?>? headers =
            CreateHeadersSnapshot(
                delivery.BasicProperties.Headers);

        Guid eventId =
            ParseGuid(
                delivery.BasicProperties.MessageId);

        Guid correlationId =
            RabbitMqTraceContext.ExtractCorrelationId(
                headers)
            ?? ParseGuid(
                delivery.BasicProperties.CorrelationId);

        using Activity? activity =
            MessagingActivity.StartConsume(
                queueName: queueName,
                routingKey: delivery.RoutingKey,
                eventType: eventType,
                eventId: eventId,
                correlationId: correlationId,
                headers: headers);

        Dictionary<string, object?> logScopeState = new()
        {
            ["CorrelationId"] =
                correlationId == Guid.Empty
                    ? null
                    : correlationId,

            ["EventId"] =
                eventId == Guid.Empty
                    ? null
                    : eventId,

            ["EventType"] = eventType,
            ["QueueName"] = queueName,
            ["RoutingKey"] = delivery.RoutingKey,
            ["DeliveryTag"] = delivery.DeliveryTag
        };

        using IDisposable? logScope =
            logger.BeginScope(logScopeState);

        try
        {
            MessageEnvelope<TEvent> envelope =
                serializer.Deserialize<MessageEnvelope<TEvent>>(
                    delivery.Body.Span);

            eventId =
                envelope.Payload.EventId;

            correlationId =
                correlationId != Guid.Empty
                    ? correlationId
                    : envelope.Payload.CorrelationId;

            logScopeState["EventId"] =
                eventId;

            logScopeState["CorrelationId"] =
                correlationId;

            activity?.SetTag(
                "messaging.message.id",
                eventId.ToString("D"));

            activity?.SetTag(
                "messaging.message.type",
                eventType);

            activity?.SetTag(
                "eshop.correlation_id",
                correlationId.ToString("D"));

            await using AsyncServiceScope scope =
                scopeFactory.CreateAsyncScope();

            NotificationEventProcessingService processingService =
                scope.ServiceProvider
                    .GetRequiredService<
                        NotificationEventProcessingService>();

            await DispatchAsync(
                processingService,
                envelope.Payload,
                _stoppingToken);

            await channel.BasicAckAsync(
                delivery.DeliveryTag,
                multiple: false);

            outcome = "success";

            activity?.SetStatus(
                ActivityStatusCode.Ok);

            MessagingTelemetry.ConsumedMessages.Add(
                1,
                CreateMetricTags(
                    queueName,
                    delivery.RoutingKey,
                    eventType,
                    outcome));
        }
        catch (OperationCanceledException)
            when (_stoppingToken.IsCancellationRequested)
        {
            outcome = "cancelled";

            activity?.SetTag(
                "messaging.operation.cancelled",
                true);

            // Zpráva zůstane bez ACK/NACK.
            // RabbitMQ ji po uzavření channelu znovu doručí.
        }
        catch (JsonException exception)
        {
            outcome = "dead_letter";
            processingException = exception;

            MessagingActivity.RecordFailure(
                activity,
                exception);

            LogInvalidJson(
                logger,
                queueName,
                delivery.DeliveryTag,
                exception);

            RecordDeadLetter(
                queueName,
                delivery.RoutingKey,
                eventType,
                exception);

            await channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: false);
        }
        catch (DbUpdateException exception)
            when (InboxDuplicateDetector.IsDuplicate(exception))
        {
            outcome = "duplicate";

            activity?.SetTag(
                "messaging.message.duplicate",
                true);

            activity?.SetStatus(
                ActivityStatusCode.Ok);

            LogDuplicateMessage(
                logger,
                queueName,
                delivery.DeliveryTag,
                exception);

            TagList tags =
                CreateMetricTags(
                    queueName,
                    delivery.RoutingKey,
                    eventType,
                    outcome);

            MessagingTelemetry.ConsumedMessages.Add(
                1,
                tags);

            MessagingTelemetry.DuplicateMessages.Add(
                1,
                tags);

            await channel.BasicAckAsync(
                delivery.DeliveryTag,
                multiple: false);
        }
        catch (DbUpdateException exception)
            when (InboxDuplicateDetector.IsTransient(exception))
        {
            outcome = "retry";
            processingException = exception;

            MessagingActivity.RecordFailure(
                activity,
                exception);

            LogTransientFailure(
                logger,
                queueName,
                delivery.DeliveryTag,
                exception);

            RecordRetry(
                queueName,
                delivery.RoutingKey,
                eventType,
                exception);

            await channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: true);
        }
        catch (ArgumentException exception)
        {
            outcome = "dead_letter";
            processingException = exception;

            MessagingActivity.RecordFailure(
                activity,
                exception);

            LogPermanentFailure(
                logger,
                queueName,
                delivery.DeliveryTag,
                exception);

            RecordDeadLetter(
                queueName,
                delivery.RoutingKey,
                eventType,
                exception);

            await channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: false);
        }
        catch (InvalidOperationException exception)
        {
            outcome = "dead_letter";
            processingException = exception;

            MessagingActivity.RecordFailure(
                activity,
                exception);

            LogPermanentFailure(
                logger,
                queueName,
                delivery.DeliveryTag,
                exception);

            RecordDeadLetter(
                queueName,
                delivery.RoutingKey,
                eventType,
                exception);

            await channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: false);
        }
        catch (NpgsqlException exception)
            when (exception.IsTransient)
        {
            outcome = "retry";
            processingException = exception;

            MessagingActivity.RecordFailure(
                activity,
                exception);

            LogTransientFailure(
                logger,
                queueName,
                delivery.DeliveryTag,
                exception);

            RecordRetry(
                queueName,
                delivery.RoutingKey,
                eventType,
                exception);

            await channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: true);
        }
        catch (TimeoutException exception)
        {
            outcome = "retry";
            processingException = exception;

            MessagingActivity.RecordFailure(
                activity,
                exception);

            LogTransientFailure(
                logger,
                queueName,
                delivery.DeliveryTag,
                exception);

            RecordRetry(
                queueName,
                delivery.RoutingKey,
                eventType,
                exception);

            await channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: true);
        }
        catch (Exception exception)
        {
            outcome = "retry";
            processingException = exception;

            MessagingActivity.RecordFailure(
                activity,
                exception);

            LogProcessingFailed(
                logger,
                queueName,
                delivery.DeliveryTag,
                exception);

            RecordRetry(
                queueName,
                delivery.RoutingKey,
                eventType,
                exception);

            await channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: true);
        }
        finally
        {
            MessagingTelemetry.ConsumeDuration.Record(
                Stopwatch
                    .GetElapsedTime(startedTimestamp)
                    .TotalMilliseconds,
                CreateMetricTags(
                    queueName,
                    delivery.RoutingKey,
                    eventType,
                    outcome,
                    processingException));
        }
    }

    private static Task DispatchAsync<TEvent>(
        NotificationEventProcessingService processingService,
        TEvent integrationEvent,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        return integrationEvent switch
        {
            OrderCreatedV1 value =>
                processingService.ProcessAsync(
                    value,
                    cancellationToken),

            StockReservedV1 value =>
                processingService.ProcessAsync(
                    value,
                    cancellationToken),

            StockReservationFailedV1 value =>
                processingService.ProcessAsync(
                    value,
                    cancellationToken),

            PaymentAuthorizedV1 value =>
                processingService.ProcessAsync(
                    value,
                    cancellationToken),

            PaymentFailedV1 value =>
                processingService.ProcessAsync(
                    value,
                    cancellationToken),

            OrderConfirmedV1 value =>
                processingService.ProcessAsync(
                    value,
                    cancellationToken),

            OrderCancelledV1 value =>
                processingService.ProcessAsync(
                    value,
                    cancellationToken),

            _ => throw new InvalidOperationException(
                $"Unsupported notification event type " +
                $"'{typeof(TEvent).Name}'.")
        };
    }

    private static void RecordRetry(
        string queueName,
        string routingKey,
        string eventType,
        Exception exception)
    {
        TagList tags =
            CreateMetricTags(
                queueName,
                routingKey,
                eventType,
                "retry",
                exception);

        MessagingTelemetry.RetriedMessages.Add(
            1,
            tags);

        MessagingTelemetry.FailedMessages.Add(
            1,
            tags);
    }

    private static void RecordDeadLetter(
        string queueName,
        string routingKey,
        string eventType,
        Exception exception)
    {
        TagList tags =
            CreateMetricTags(
                queueName,
                routingKey,
                eventType,
                "dead_letter",
                exception);

        MessagingTelemetry.DeadLetteredMessages.Add(
            1,
            tags);

        MessagingTelemetry.FailedMessages.Add(
            1,
            tags);
    }

    private static TagList CreateMetricTags(
        string queueName,
        string routingKey,
        string eventType,
        string outcome,
        Exception? exception = null)
    {
        TagList tags = new()
        {
            {
                "messaging.system",
                "rabbitmq"
            },
            {
                "messaging.destination.name",
                queueName
            },
            {
                "messaging.rabbitmq.routing_key",
                routingKey
            },
            {
                "messaging.message.type",
                eventType
            },
            {
                "messaging.operation.type",
                "process"
            },
            {
                "messaging.operation.outcome",
                outcome
            }
        };

        if (exception is not null)
        {
            tags.Add(
                "error.type",
                exception.GetType().Name);
        }

        return tags;
    }

    private static Dictionary<string, object?>?
        CreateHeadersSnapshot(
            IDictionary<string, object?>? headers)
    {
        if (headers is null)
        {
            return null;
        }

        return new Dictionary<string, object?>(
            headers,
            StringComparer.OrdinalIgnoreCase);
    }

    private static Guid ParseGuid(
        string? value)
    {
        return Guid.TryParse(
            value,
            out Guid parsedValue)
                ? parsedValue
                : Guid.Empty;
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await base.StopAsync(
                cancellationToken);
        }
        finally
        {
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
                _channel = null;
            }
        }
    }
}
