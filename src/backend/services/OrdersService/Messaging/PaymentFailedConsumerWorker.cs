using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.Contracts;
using Messaging.Shared.RabbitMq;
using Messaging.Shared.Serialization;
using Messaging.Shared.Telemetry;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrdersService.Application;
using OrdersService.Inbox;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrdersService.Messaging;

public sealed class PaymentFailedConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IRabbitMqConnectionProvider connectionProvider,
    IMessageSerializer serializer,
    ILogger<PaymentFailedConsumerWorker> logger)
    : BackgroundService
{
    private const string QueueName =
        RabbitMqQueues.OrdersPaymentFailedV1;

    private const string EventType =
        nameof(PaymentFailedV1);

    private static readonly Action<ILogger, ulong, Exception?>
        LogInvalidJson =
            LoggerMessage.Define<ulong>(
                LogLevel.Error,
                new EventId(3600, nameof(LogInvalidJson)),
                "PaymentFailed message {DeliveryTag} contains invalid JSON.");

    private static readonly Action<ILogger, ulong, Exception?>
        LogProcessingFailed =
            LoggerMessage.Define<ulong>(
                LogLevel.Error,
                new EventId(3601, nameof(LogProcessingFailed)),
                "PaymentFailed message {DeliveryTag} processing failed.");

    private static readonly Action<ILogger, ulong, Exception?>
        LogPermanentFailure =
            LoggerMessage.Define<ulong>(
                LogLevel.Error,
                new EventId(3602, nameof(LogPermanentFailure)),
                "PaymentFailed message {DeliveryTag} failed with a permanent processing error and will be dead-lettered.");

    private static readonly Action<ILogger, ulong, Exception?>
        LogTransientFailure =
            LoggerMessage.Define<ulong>(
                LogLevel.Warning,
                new EventId(3603, nameof(LogTransientFailure)),
                "PaymentFailed message {DeliveryTag} failed with a transient processing error and will be retried.");

    private static readonly Action<ILogger, ulong, Exception?>
        LogDuplicateMessage =
            LoggerMessage.Define<ulong>(
                LogLevel.Information,
                new EventId(3604, nameof(LogDuplicateMessage)),
                "PaymentFailed message {DeliveryTag} was already processed by another consumer instance.");

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
            prefetchCount: 8,
            global: false,
            cancellationToken: stoppingToken);

        AsyncEventingBasicConsumer consumer =
            new(_channel);

        consumer.ReceivedAsync += HandleDeliveryAsync;

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(
            Timeout.InfiniteTimeSpan,
            stoppingToken);
    }

    private async Task HandleDeliveryAsync(
        object sender,
        BasicDeliverEventArgs delivery)
    {
        IChannel? channel = _channel;

        if (channel is null)
        {
            return;
        }

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
                queueName: QueueName,
                routingKey: delivery.RoutingKey,
                eventType: EventType,
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

            ["EventType"] = EventType,
            ["QueueName"] = QueueName,
            ["RoutingKey"] = delivery.RoutingKey,
            ["DeliveryTag"] = delivery.DeliveryTag
        };

        using IDisposable? logScope =
            logger.BeginScope(logScopeState);

        try
        {
            MessageEnvelope<PaymentFailedV1> envelope =
                serializer.Deserialize<
                    MessageEnvelope<PaymentFailedV1>>(
                    delivery.Body.Span);

            eventId = envelope.Payload.EventId;

            correlationId =
                correlationId != Guid.Empty
                    ? correlationId
                    : envelope.Payload.CorrelationId;

            logScopeState["EventId"] = eventId;
            logScopeState["CorrelationId"] = correlationId;

            activity?.SetTag(
                "messaging.message.id",
                eventId.ToString("D"));

            activity?.SetTag(
                "eshop.correlation_id",
                correlationId.ToString("D"));

            await using AsyncServiceScope scope =
                scopeFactory.CreateAsyncScope();

            OrderPaymentResultService service =
                scope.ServiceProvider
                    .GetRequiredService<
                        OrderPaymentResultService>();

            await service.ApplyPaymentFailedAsync(
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
                    delivery.RoutingKey,
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
                delivery.DeliveryTag,
                exception);

            RecordDeadLetter(
                delivery.RoutingKey,
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
                delivery.DeliveryTag,
                exception);

            TagList tags =
                CreateMetricTags(
                    delivery.RoutingKey,
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
                delivery.DeliveryTag,
                exception);

            RecordRetry(
                delivery.RoutingKey,
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
                delivery.DeliveryTag,
                exception);

            RecordDeadLetter(
                delivery.RoutingKey,
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
                delivery.DeliveryTag,
                exception);

            RecordDeadLetter(
                delivery.RoutingKey,
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
                delivery.DeliveryTag,
                exception);

            RecordRetry(
                delivery.RoutingKey,
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
                delivery.DeliveryTag,
                exception);

            RecordRetry(
                delivery.RoutingKey,
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
                delivery.DeliveryTag,
                exception);

            RecordRetry(
                delivery.RoutingKey,
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
                    delivery.RoutingKey,
                    outcome,
                    processingException));
        }
    }

    private static void RecordRetry(
        string routingKey,
        Exception exception)
    {
        TagList tags =
            CreateMetricTags(
                routingKey,
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
        string routingKey,
        Exception exception)
    {
        TagList tags =
            CreateMetricTags(
                routingKey,
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
        string routingKey,
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
                QueueName
            },
            {
                "messaging.rabbitmq.routing_key",
                routingKey
            },
            {
                "messaging.message.type",
                EventType
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

    private static Guid ParseGuid(string? value)
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
