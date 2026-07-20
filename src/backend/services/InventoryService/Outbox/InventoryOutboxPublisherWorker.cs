using System.Text.Json;
using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.Abstractions;
using Messaging.Shared.Contracts;
using Messaging.Shared.Outbox;
using Messaging.Shared.RabbitMq;
using Microsoft.Extensions.Options;

namespace InventoryService.Outbox;

public sealed class InventoryOutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    IIntegrationEventPublisher eventPublisher,
    TimeProvider timeProvider,
    IOptions<OutboxProcessingOptions> options,
    ILogger<InventoryOutboxPublisherWorker> logger)
    : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    private static readonly Action<ILogger, Exception?>
        LogBatchFailed =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(2100, nameof(LogBatchFailed)),
                "Inventory outbox publishing batch failed.");

    private static readonly Action<ILogger, Guid, Guid, Exception?>
        LogMessageFailed =
            LoggerMessage.Define<Guid, Guid>(
                LogLevel.Error,
                new EventId(2101, nameof(LogMessageFailed)),
                "Inventory outbox message {MessageId} with event {EventId} failed.");

    private static readonly Action<ILogger, Guid, string, Exception?>
        LogClaimLost =
            LoggerMessage.Define<Guid, string>(
                LogLevel.Warning,
                new EventId(2102, nameof(LogClaimLost)),
                "Inventory outbox message {MessageId} is no longer claimed by worker {WorkerId}.");

    private static readonly Action<ILogger, Guid, Exception?>
        LogStateUpdateFailed =
            LoggerMessage.Define<Guid>(
                LogLevel.Error,
                new EventId(2103, nameof(LogStateUpdateFailed)),
                "Updating state of inventory outbox message {MessageId} failed.");

    private readonly OutboxProcessingOptions _options =
        options.Value;

    private readonly string _workerId =
        $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processedCount =
                    await PublishBatchAsync(
                        stoppingToken);

                if (processedCount == 0)
                {
                    await Task.Delay(
                        _options.PollingInterval,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogBatchFailed(
                    logger,
                    exception);

                await Task.Delay(
                    _options.PollingInterval,
                    stoppingToken);
            }
        }
    }

    private async Task<int> PublishBatchAsync(
        CancellationToken cancellationToken)
    {
        List<ClaimedOutboxMessage> messages =
            await ClaimBatchAsync(
                cancellationToken);

        foreach (ClaimedOutboxMessage message in messages)
        {
            await ProcessMessageAsync(
                message,
                cancellationToken);
        }

        return messages.Count;
    }

    private async Task ProcessMessageAsync(
        ClaimedOutboxMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await PublishMessageAsync(
                message,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogMessageFailed(
                logger,
                message.Id,
                message.EventId,
                exception);

            try
            {
                bool markedFailed =
                    await MarkFailedAsync(
                        message.Id,
                        exception.Message,
                        cancellationToken);

                if (!markedFailed)
                {
                    LogClaimLost(
                        logger,
                        message.Id,
                        _workerId,
                        null);
                }
            }
            catch (Exception updateException)
            {
                LogStateUpdateFailed(
                    logger,
                    message.Id,
                    updateException);

                throw;
            }

            return;
        }

        try
        {
            bool markedPublished =
                await MarkPublishedAsync(
                    message.Id,
                    timeProvider.GetUtcNow(),
                    cancellationToken);

            if (!markedPublished)
            {
                LogClaimLost(
                    logger,
                    message.Id,
                    _workerId,
                    null);
            }
        }
        catch (Exception exception)
        {
            LogStateUpdateFailed(
                logger,
                message.Id,
                exception);

            throw;
        }
    }

    private async Task<List<ClaimedOutboxMessage>>
        ClaimBatchAsync(
            CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
            scopeFactory.CreateAsyncScope();

        InventoryOutboxStore store =
            scope.ServiceProvider
                .GetRequiredService<InventoryOutboxStore>();

        return await store.ClaimBatchAsync(
            _workerId,
            cancellationToken);
    }

    private async Task<bool> MarkPublishedAsync(
        Guid messageId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
            scopeFactory.CreateAsyncScope();

        InventoryOutboxStore store =
            scope.ServiceProvider
                .GetRequiredService<InventoryOutboxStore>();

        return await store.MarkPublishedAsync(
            messageId,
            _workerId,
            publishedAtUtc,
            cancellationToken);
    }

    private async Task<bool> MarkFailedAsync(
        Guid messageId,
        string error,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
            scopeFactory.CreateAsyncScope();

        InventoryOutboxStore store =
            scope.ServiceProvider
                .GetRequiredService<InventoryOutboxStore>();

        return await store.MarkFailedAsync(
            messageId,
            _workerId,
            error,
            cancellationToken);
    }

    private Task PublishMessageAsync(
        ClaimedOutboxMessage message,
        CancellationToken cancellationToken)
    {
        MessagePublishContext context = new(
            message.CorrelationId,
            message.TraceParent,
            message.TraceState);

        return message.RoutingKey switch
        {
            RabbitMqRoutingKeys.StockReservedV1 =>
                eventPublisher.PublishAsync(
                    Deserialize<StockReservedV1>(
                        message.Payload),
                    message.RoutingKey,
                    context,
                    cancellationToken),

            RabbitMqRoutingKeys.StockReservationFailedV1 =>
                eventPublisher.PublishAsync(
                    Deserialize<StockReservationFailedV1>(
                        message.Payload),
                    message.RoutingKey,
                    context,
                    cancellationToken),

            RabbitMqRoutingKeys.StockReleasedV1 =>
                eventPublisher.PublishAsync(
                    Deserialize<StockReleasedV1>(
                        message.Payload),
                    message.RoutingKey,
                    context,
                    cancellationToken),

            _ => throw new InvalidOperationException(
                $"Unsupported inventory outbox routing key " +
                $"'{message.RoutingKey}'.")
        };
    }

    private static TEvent Deserialize<TEvent>(
        string payload)
    {
        return JsonSerializer.Deserialize<TEvent>(
            payload,
            SerializerOptions)
            ?? throw new JsonException(
                $"Outbox payload could not be deserialized " +
                $"as '{typeof(TEvent).Name}'.");
    }
}
