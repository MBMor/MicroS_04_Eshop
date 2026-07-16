using System.Text.Json;
using Eshop.Contracts.IntegrationEvents.V1;
using InventoryService.Data;
using Messaging.Shared.Abstractions;
using Messaging.Shared.Contracts;
using Messaging.Shared.RabbitMq;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Outbox;

public sealed class InventoryOutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    IIntegrationEventPublisher eventPublisher,
    TimeProvider timeProvider,
    ILogger<InventoryOutboxPublisherWorker> logger)
    : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int count = await PublishBatchAsync(stoppingToken);

                if (count == 0)
                {
                    await Task.Delay(PollingInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Inventory outbox publishing batch failed.");
                await Task.Delay(PollingInterval, stoppingToken);
            }
        }
    }

    private async Task<int> PublishBatchAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        InventoryDbContext dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        List<OutboxMessage> messages = await dbContext.OutboxMessages
            .Where(message => message.Status != OutboxMessageStatus.Published)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (OutboxMessage message in messages)
        {
            try
            {
                await PublishMessageAsync(message, cancellationToken);
                message.MarkPublished(timeProvider.GetUtcNow());
            }
            catch (Exception exception)
            {
                message.MarkFailed(exception.Message);
                logger.LogError(exception, "Inventory outbox message {MessageId} failed.", message.Id);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return messages.Count;
    }

    private Task PublishMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        MessagePublishContext context = new(message.TraceParent, message.TraceState);

        return message.RoutingKey switch
        {
            RabbitMqRoutingKeys.StockReservedV1 => eventPublisher.PublishAsync(
                Deserialize<StockReservedV1>(message.Payload),
                message.RoutingKey,
                context,
                cancellationToken),

            RabbitMqRoutingKeys.StockReservationFailedV1 => eventPublisher.PublishAsync(
                Deserialize<StockReservationFailedV1>(message.Payload),
                message.RoutingKey,
                context,
                cancellationToken),

            _ => throw new InvalidOperationException(
                $"Unsupported inventory outbox routing key '{message.RoutingKey}'.")
        };
    }

    private static TEvent Deserialize<TEvent>(string payload)
    {
        return JsonSerializer.Deserialize<TEvent>(payload, SerializerOptions)
            ?? throw new JsonException($"Outbox payload could not be deserialized as '{typeof(TEvent).Name}'.");
    }
}
