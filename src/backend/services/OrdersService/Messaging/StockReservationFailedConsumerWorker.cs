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
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IConnection connection = await connectionProvider.GetConnectionAsync(stoppingToken);

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

            logger.LogWarning(
                "Stock reservation failed for order {OrderId}: {Reason}",
                envelope.Payload.OrderId,
                envelope.Payload.Reason);

            await _channel.BasicAckAsync(
                delivery.DeliveryTag,
                multiple: false);
        }
        catch (JsonException exception)
        {
            logger.LogError(
                exception,
                "StockReservationFailed message {DeliveryTag} contains invalid JSON.",
                delivery.DeliveryTag);

            await _channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: false);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "StockReservationFailed message {DeliveryTag} processing failed.",
                delivery.DeliveryTag);

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
