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
    private static readonly Action<ILogger, ulong, Exception?> LogInvalidJson =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(2200, nameof(LogInvalidJson)),
            "StockReserved message {DeliveryTag} contains invalid JSON.");

    private static readonly Action<ILogger, ulong, Exception?> LogProcessingFailed =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(2201, nameof(LogProcessingFailed)),
            "StockReserved message {DeliveryTag} processing failed.");

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
            queue: RabbitMqQueues.OrdersStockReservedV1,
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
            MessageEnvelope<StockReservedV1> envelope =
                serializer.Deserialize<MessageEnvelope<StockReservedV1>>(
                    delivery.Body.Span);

            await using AsyncServiceScope scope =
                scopeFactory.CreateAsyncScope();

            OrderStockResultService service =
                scope.ServiceProvider
                    .GetRequiredService<OrderStockResultService>();

            await service.ApplyStockReservedAsync(
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
                requeue: true);
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
