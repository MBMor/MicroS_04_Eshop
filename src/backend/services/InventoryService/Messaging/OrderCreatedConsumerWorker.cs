using System.Text.Json;
using Eshop.Contracts.IntegrationEvents.V1;
using InventoryService.Application;
using Messaging.Shared.Contracts;
using Messaging.Shared.RabbitMq;
using Messaging.Shared.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace InventoryService.Messaging;

public sealed class OrderCreatedConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IRabbitMqConnectionProvider connectionProvider,
    IMessageSerializer serializer,
    ILogger<OrderCreatedConsumerWorker> logger)
    : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IConnection connection = await connectionProvider.GetConnectionAsync(stoppingToken);
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 8,
            global: false,
            cancellationToken: stoppingToken);

        AsyncEventingBasicConsumer consumer = new(_channel);
        consumer.ReceivedAsync += HandleDeliveryAsync;

        await _channel.BasicConsumeAsync(
            queue: RabbitMqQueues.InventoryOrderCreatedV1,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleDeliveryAsync(object sender, BasicDeliverEventArgs delivery)
    {
        if (_channel is null)
        {
            return;
        }

        try
        {
            MessageEnvelope<OrderCreatedV1> envelope =
                serializer.Deserialize<MessageEnvelope<OrderCreatedV1>>(delivery.Body.Span);

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

            OrderStockReservationService reservationService =
                scope.ServiceProvider.GetRequiredService<OrderStockReservationService>();

            await reservationService.ReserveAsync(
                envelope.Payload,
                CancellationToken.None);

            await _channel.BasicAckAsync(
                delivery.DeliveryTag,
                multiple: false);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "OrderCreated message {DeliveryTag} contains invalid JSON.", delivery.DeliveryTag);

            await _channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "OrderCreated message {DeliveryTag} processing failed.", delivery.DeliveryTag);

            await _channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
