using System.Text.Json;
using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.Contracts;
using Messaging.Shared.RabbitMq;
using Messaging.Shared.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaymentsService.Application;
using PaymentsService.Inbox;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PaymentsService.Messaging;

public sealed class PaymentRequestedConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IRabbitMqConnectionProvider connectionProvider,
    IMessageSerializer serializer,
    ILogger<PaymentRequestedConsumerWorker> logger)
    : BackgroundService
{
    private static readonly Action<ILogger, ulong, Exception?> LogInvalidJson =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(3000, nameof(LogInvalidJson)),
            "PaymentRequested message {DeliveryTag} contains invalid JSON.");

    private static readonly Action<ILogger, ulong, Exception?> LogProcessingFailed =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(3001, nameof(LogProcessingFailed)),
            "PaymentRequested message {DeliveryTag} processing failed.");

    private static readonly Action<ILogger, ulong, Exception?> LogPermanentFailure =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(3002, nameof(LogPermanentFailure)),
            "PaymentRequested message {DeliveryTag} failed permanently and will be dead-lettered.");

    private static readonly Action<ILogger, ulong, Exception?> LogTransientFailure =
        LoggerMessage.Define<ulong>(
            LogLevel.Warning,
            new EventId(3003, nameof(LogTransientFailure)),
            "PaymentRequested message {DeliveryTag} failed transiently and will be retried.");

    private static readonly Action<ILogger, ulong, Exception?> LogDuplicateMessage =
        LoggerMessage.Define<ulong>(
            LogLevel.Information,
            new EventId(3004, nameof(LogDuplicateMessage)),
            "PaymentRequested message {DeliveryTag} was already processed by another consumer instance.");

    private IChannel? _channel;
    private CancellationToken _stoppingToken;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

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
            queue: RabbitMqQueues.PaymentsPaymentRequestedV1,
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
            MessageEnvelope<PaymentRequestedV1> envelope =
                serializer.Deserialize<MessageEnvelope<PaymentRequestedV1>>(
                    delivery.Body.Span);

            await using AsyncServiceScope scope =
                scopeFactory.CreateAsyncScope();

            PaymentRequestedProcessingService service =
                scope.ServiceProvider
                    .GetRequiredService<PaymentRequestedProcessingService>();

            await service.ProcessAsync(
                envelope.Payload,
                _stoppingToken);

            await _channel.BasicAckAsync(
                delivery.DeliveryTag,
                multiple: false);
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
