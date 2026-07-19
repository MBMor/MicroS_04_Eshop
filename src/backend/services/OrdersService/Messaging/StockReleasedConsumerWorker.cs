using System.Text.Json;
using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.Contracts;
using Messaging.Shared.RabbitMq;
using Messaging.Shared.Serialization;
using OrdersService.Application;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrdersService.Inbox;

namespace OrdersService.Messaging;

public sealed class StockReleasedConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IRabbitMqConnectionProvider connectionProvider,
    IMessageSerializer serializer,
    ILogger<StockReleasedConsumerWorker> logger)
    : BackgroundService
{
    private static readonly Action<ILogger, ulong, Exception?> LogInvalidJson =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(3500, nameof(LogInvalidJson)),
            "StockReleased message {DeliveryTag} contains invalid JSON.");

    private static readonly Action<ILogger, ulong, Exception?> LogProcessingFailed =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(3501, nameof(LogProcessingFailed)),
            "StockReleased message {DeliveryTag} processing failed.");

    private static readonly Action<ILogger, ulong, Exception?> LogPermanentFailure =
    LoggerMessage.Define<ulong>(
        LogLevel.Error,
        new EventId(9000, nameof(LogPermanentFailure)),
        "Message {DeliveryTag} failed with a permanent processing error and will be dead-lettered.");

    private static readonly Action<ILogger, ulong, Exception?> LogTransientFailure =
        LoggerMessage.Define<ulong>(
            LogLevel.Warning,
            new EventId(9001, nameof(LogTransientFailure)),
            "Message {DeliveryTag} failed with a transient processing error and will be retried.");

    private static readonly Action<ILogger, ulong, Exception?> LogDuplicateMessage =
        LoggerMessage.Define<ulong>(
            LogLevel.Information,
            new EventId(9002, nameof(LogDuplicateMessage)),
            "Message {DeliveryTag} was already processed by another consumer instance.");

    private IChannel? _channel;
    private CancellationToken _stoppingToken;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        IConnection connection =
            await connectionProvider.GetConnectionAsync(stoppingToken);
        _stoppingToken = stoppingToken;

        _channel = await connection.CreateChannelAsync(
            cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(0, 8, false, stoppingToken);

        AsyncEventingBasicConsumer consumer = new(_channel);
        consumer.ReceivedAsync += HandleDeliveryAsync;

        await _channel.BasicConsumeAsync(
            RabbitMqQueues.OrdersStockReleasedV1,
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
            MessageEnvelope<StockReleasedV1> envelope =
                serializer.Deserialize<MessageEnvelope<StockReleasedV1>>(
                    delivery.Body.Span);

            await using AsyncServiceScope scope =
                scopeFactory.CreateAsyncScope();

            OrderPaymentResultService service =
                scope.ServiceProvider
                    .GetRequiredService<OrderPaymentResultService>();

            await service.ApplyStockReleasedAsync(
                envelope.Payload,
                _stoppingToken);

            await _channel.BasicAckAsync(
                delivery.DeliveryTag,
                false);
        }
        catch (OperationCanceledException)
            when (_stoppingToken.IsCancellationRequested)
        {
            return;
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
        catch (DbUpdateException exception)
            when (InboxDuplicateDetector.IsDuplicate(exception))
        {
            LogDuplicateMessage(
                logger,
                delivery.DeliveryTag,
                exception);

            await _channel.BasicAckAsync(
                delivery.DeliveryTag,
                multiple: false);
        }
        catch (DbUpdateException exception)
            when (InboxDuplicateDetector.IsTransient(exception))
        {
            LogTransientFailure(
                logger,
                delivery.DeliveryTag,
                exception);

            await _channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: true);
        }
        catch (ArgumentException exception)
        {
            LogPermanentFailure(
                logger,
                delivery.DeliveryTag,
                exception);

            await _channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: false);
        }
        catch (InvalidOperationException exception)
        {
            LogPermanentFailure(
                logger,
                delivery.DeliveryTag,
                exception);

            await _channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: false);
        }
        catch (NpgsqlException exception)
            when (exception.IsTransient)
        {
            LogTransientFailure(
                logger,
                delivery.DeliveryTag,
                exception);

            await _channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: true);
        }
        catch (TimeoutException exception)
        {
            LogTransientFailure(
                logger,
                delivery.DeliveryTag,
                exception);

            await _channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: true);
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
        try
        {
            await base.StopAsync(cancellationToken);
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
