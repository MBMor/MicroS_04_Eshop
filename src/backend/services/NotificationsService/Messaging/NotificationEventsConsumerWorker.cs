using System.Text.Json;
using Eshop.Contracts.IntegrationEvents;
using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.Contracts;
using Messaging.Shared.RabbitMq;
using Messaging.Shared.Serialization;
using NotificationsService.Application;
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
    private static readonly Action<ILogger, string, ulong, Exception?> LogInvalidJson =
        LoggerMessage.Define<string, ulong>(
            LogLevel.Error,
            new EventId(4000, nameof(LogInvalidJson)),
            "Notification message from queue {QueueName} with delivery tag {DeliveryTag} contains invalid JSON.");

    private static readonly Action<ILogger, string, ulong, Exception?> LogProcessingFailed =
        LoggerMessage.Define<string, ulong>(
            LogLevel.Error,
            new EventId(4001, nameof(LogProcessingFailed)),
            "Notification message from queue {QueueName} with delivery tag {DeliveryTag} failed.");

    private IChannel? _channel;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        IConnection connection =
            await connectionProvider.GetConnectionAsync(stoppingToken);

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
        if (_channel is null)
        {
            throw new InvalidOperationException(
                "RabbitMQ channel has not been initialized.");
        }

        AsyncEventingBasicConsumer consumer = new(_channel);

        consumer.ReceivedAsync += (_, delivery) =>
            HandleDeliveryAsync<TEvent>(
                queueName,
                delivery);

        await _channel.BasicConsumeAsync(
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
        if (_channel is null)
        {
            return;
        }

        try
        {
            MessageEnvelope<TEvent> envelope =
                serializer.Deserialize<MessageEnvelope<TEvent>>(
                    delivery.Body.Span);

            await using AsyncServiceScope scope =
                scopeFactory.CreateAsyncScope();

            NotificationEventProcessingService processingService =
                scope.ServiceProvider
                    .GetRequiredService<NotificationEventProcessingService>();

            await DispatchAsync(
                processingService,
                envelope.Payload,
                CancellationToken.None);

            await _channel.BasicAckAsync(
                delivery.DeliveryTag,
                multiple: false);
        }
        catch (JsonException exception)
        {
            LogInvalidJson(
                logger,
                queueName,
                delivery.DeliveryTag,
                exception);

            await _channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: false);
        }
        catch (Exception exception)
        {
            LogProcessingFailed(
                logger,
                queueName,
                delivery.DeliveryTag,
                exception);

            await _channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: true);
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
                processingService.ProcessAsync(value, cancellationToken),

            StockReservedV1 value =>
                processingService.ProcessAsync(value, cancellationToken),

            StockReservationFailedV1 value =>
                processingService.ProcessAsync(value, cancellationToken),

            PaymentAuthorizedV1 value =>
                processingService.ProcessAsync(value, cancellationToken),

            PaymentFailedV1 value =>
                processingService.ProcessAsync(value, cancellationToken),

            OrderConfirmedV1 value =>
                processingService.ProcessAsync(value, cancellationToken),

            OrderCancelledV1 value =>
                processingService.ProcessAsync(value, cancellationToken),

            _ => throw new InvalidOperationException(
                $"Unsupported notification event '{typeof(TEvent).Name}'.")
        };
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        await base.StopAsync(cancellationToken);
    }
}
