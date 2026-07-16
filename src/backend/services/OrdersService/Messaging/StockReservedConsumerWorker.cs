using System.Text.Json;
using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.Contracts;
using Messaging.Shared.RabbitMq;
using Messaging.Shared.Serialization;
using OrdersService.Application;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrdersService.Messaging;

public sealed class StockReservedConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IRabbitMqConnectionProvider connectionProvider,
    IMessageSerializer serializer,
    ILogger<StockReservedConsumerWorker> logger)
    : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IConnection connection = await connectionProvider.GetConnectionAsync(stoppingToken);
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(0, 8, false, stoppingToken);

        AsyncEventingBasicConsumer consumer = new(_channel);
        consumer.ReceivedAsync += HandleDeliveryAsync;

        await _channel.BasicConsumeAsync(
            RabbitMqQueues.OrdersStockReservedV1,
            autoAck: false,
            consumer,
            stoppingToken);

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
            MessageEnvelope<StockReservedV1> envelope =
                serializer.Deserialize<MessageEnvelope<StockReservedV1>>(delivery.Body.Span);

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

            OrderStockResultService service =
                scope.ServiceProvider.GetRequiredService<OrderStockResultService>();

            await service.ApplyStockReservedAsync(
                envelope.Payload.OrderId,
                CancellationToken.None);

            await _channel.BasicAckAsync(delivery.DeliveryTag, false);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "StockReserved message contains invalid JSON.");
            await _channel.BasicNackAsync(delivery.DeliveryTag, false, false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "StockReserved message processing failed.");
            await _channel.BasicNackAsync(delivery.DeliveryTag, false, false);
        }
    }
}
