using System.Text.Json;
using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.Abstractions;
using Messaging.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using Messaging.Shared.RabbitMq;


namespace OrdersService.Outbox;

public sealed class OrdersOutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    IIntegrationEventPublisher eventPublisher,
    TimeProvider timeProvider,
    ILogger<OrdersOutboxPublisherWorker> logger)
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
                int processedCount = await PublishBatchAsync(stoppingToken);

                if (processedCount == 0)
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
                logger.LogError(exception, "Orders outbox publishing batch failed.");
                await Task.Delay(PollingInterval, stoppingToken);
            }
        }
    }

    private async Task<int> PublishBatchAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        OrdersDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

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

                logger.LogError(
                    exception,
                    "Publishing orders outbox message {MessageId} with event {EventId} failed.",
                    message.Id,
                    message.EventId);
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
            RabbitMqRoutingKeys.OrderCreatedV1 => eventPublisher.PublishAsync(
                Deserialize<OrderCreatedV1>(message.Payload),
                message.RoutingKey,
                context,
                cancellationToken),

            _ => throw new InvalidOperationException(
                $"Unsupported orders outbox routing key '{message.RoutingKey}'.")
        };
    }

    private static TEvent Deserialize<TEvent>(string payload)
    {
        return JsonSerializer.Deserialize<TEvent>(payload, SerializerOptions)
            ?? throw new JsonException($"Outbox payload could not be deserialized as '{typeof(TEvent).Name}'.");
    }
}
