using System.Text.Json;
using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.Contracts;
using Messaging.Shared.RabbitMq;
using Messaging.Shared.Serialization;
using OrdersService.Application;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrdersService.Messaging;

public sealed class StockReservationFailedConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IRabbitMqConnectionProvider connectionProvider,
    IMessageSerializer serializer,
    ILogger<StockReservationFailedConsumerWorker> logger)
    : BackgroundService
{
    private static readonly Action<ILogger, Guid, string, Exception?> LogReservationFailed =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Warning,
            new EventId(2300, nameof(LogReservationFailed)),
            "Stock reservation failed for order {OrderId}: {Reason}");

    private static readonly Action<ILogger, ulong, Exception?> LogInvalidJson =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(2301, nameof(LogInvalidJson)),
            "StockReservationFailed message {DeliveryTag} contains invalid JSON.");

    private static readonly Action<ILogger, ulong, Exception?> LogProcessingFailed =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(2302, nameof(LogProcessingFailed)),
            "StockReservationFailed message {DeliveryTag} processing failed.");

    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IConnection connection =
            await connectionProvider.GetConnectionAsync(stoppingToken);

        _channel = await connection.CreateChannelAsync(
            cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 8,
            global: false,
            cancellationToken: stoppingToken);

        AsyncEventingBasicConsumer consumer = new(_channel);
        consumer.ReceivedAsync += HandleDeliveryAsync;

        await _channel.BasicConsumeAsync(
            queue: RabbitMqQueues.OrdersStockReservationFailedV1,
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
        if (_channel is null)
        {
            return;
        }

        try
        {
            MessageEnvelope<StockReservationFailedV1> envelope =
                serializer.Deserialize<MessageEnvelope<StockReservationFailedV1>>(
                    delivery.Body.Span);

            await using AsyncServiceScope scope =
                scopeFactory.CreateAsyncScope();

            OrderStockResultService service =
                scope.ServiceProvider
                    .GetRequiredService<OrderStockResultService>();

            await service.ApplyStockReservationFailedAsync(
                envelope.Payload.OrderId,
                envelope.Payload.Reason,
                CancellationToken.None);

            LogReservationFailed(
                logger,
                envelope.Payload.OrderId,
                envelope.Payload.Reason,
                null);

            await _channel.BasicAckAsync(
                delivery.DeliveryTag,
                multiple: false);
        }
        catch (JsonException exception)
        {
            LogInvalidJson(
                logger,
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
                delivery.DeliveryTag,
                exception);

            await _channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: false);
        }
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
