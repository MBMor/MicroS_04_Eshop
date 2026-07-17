using System.Text.Json;
using Eshop.Contracts.IntegrationEvents.V1;
using InventoryService.Application;
using Messaging.Shared.Contracts;
using Messaging.Shared.RabbitMq;
using Messaging.Shared.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace InventoryService.Messaging;

public sealed class StockReleaseRequestedConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IRabbitMqConnectionProvider connectionProvider,
    IMessageSerializer serializer,
    ILogger<StockReleaseRequestedConsumerWorker> logger)
    : BackgroundService
{
    private static readonly Action<ILogger, ulong, Exception?> LogInvalidJson =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(3400, nameof(LogInvalidJson)),
            "StockReleaseRequested message {DeliveryTag} contains invalid JSON.");

    private static readonly Action<ILogger, ulong, Exception?> LogProcessingFailed =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(3401, nameof(LogProcessingFailed)),
            "StockReleaseRequested message {DeliveryTag} processing failed.");

    private IChannel? _channel;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        IConnection connection =
            await connectionProvider.GetConnectionAsync(stoppingToken);

        _channel = await connection.CreateChannelAsync(
            cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(0, 8, false, stoppingToken);

        AsyncEventingBasicConsumer consumer = new(_channel);
        consumer.ReceivedAsync += HandleDeliveryAsync;

        await _channel.BasicConsumeAsync(
            RabbitMqQueues.InventoryStockReleaseRequestedV1,
            autoAck: false,
            consumer,
            stoppingToken);

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
            MessageEnvelope<StockReleaseRequestedV1> envelope =
                serializer.Deserialize<MessageEnvelope<StockReleaseRequestedV1>>(
                    delivery.Body.Span);

            await using AsyncServiceScope scope =
                scopeFactory.CreateAsyncScope();

            OrderStockReleaseService service =
                scope.ServiceProvider
                    .GetRequiredService<OrderStockReleaseService>();

            await service.ReleaseAsync(
                envelope.Payload,
                CancellationToken.None);

            await _channel.BasicAckAsync(
                delivery.DeliveryTag,
                false);
        }
        catch (JsonException exception)
        {
            LogInvalidJson(logger, delivery.DeliveryTag, exception);
            await _channel.BasicNackAsync(delivery.DeliveryTag, false, false);
        }
        catch (Exception exception)
        {
            LogProcessingFailed(logger, delivery.DeliveryTag, exception);
            await _channel.BasicNackAsync(delivery.DeliveryTag, false, true);
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
