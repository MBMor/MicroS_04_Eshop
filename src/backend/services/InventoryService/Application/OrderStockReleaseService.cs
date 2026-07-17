using Eshop.Contracts.IntegrationEvents.V1;
using InventoryService.Data;
using InventoryService.Domain;
using InventoryService.Inbox;
using InventoryService.Outbox;
using Messaging.Shared.RabbitMq;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Application;

public sealed class OrderStockReleaseService(
    InventoryDbContext dbContext,
    InventoryOutboxWriter outboxWriter,
    TimeProvider timeProvider)
{
    public async Task ReleaseAsync(
        StockReleaseRequestedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        bool alreadyProcessed = await dbContext.ProcessedMessages
            .AnyAsync(
                message =>
                    message.EventId == integrationEvent.EventId
                    && message.ConsumerName == ConsumerNames.StockReleaseRequested,
                cancellationToken);

        if (alreadyProcessed)
        {
            return;
        }

        Guid[] productIds = integrationEvent.Items
            .Select(item => item.ProductId)
            .Distinct()
            .ToArray();

        List<InventoryItem> inventoryItems = await dbContext.InventoryItems
            .Where(item => productIds.Contains(item.ProductId))
            .ToListAsync(cancellationToken);

        Dictionary<Guid, InventoryItem> inventoryByProductId =
            inventoryItems.ToDictionary(item => item.ProductId);

        DateTimeOffset now = timeProvider.GetUtcNow();

        foreach (StockReleaseItemV1 requestedItem in integrationEvent.Items)
        {
            if (!inventoryByProductId.TryGetValue(
                    requestedItem.ProductId,
                    out InventoryItem? inventoryItem))
            {
                throw new InvalidOperationException(
                    $"Inventory item for product '{requestedItem.ProductId}' does not exist.");
            }

            inventoryItem.ReleaseReservation(
                requestedItem.Quantity,
                now);
        }

        StockReleasedV1 stockReleased = new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: now,
            CorrelationId: integrationEvent.CorrelationId,
            OrderId: integrationEvent.OrderId,
            CustomerId: integrationEvent.CustomerId,
            Items: integrationEvent.Items
                .Select(item => new ReleasedStockItemV1(
                    item.ProductId,
                    item.Quantity))
                .ToArray());

        dbContext.OutboxMessages.Add(
            outboxWriter.Create(
                stockReleased,
                RabbitMqRoutingKeys.StockReleasedV1));

        dbContext.ProcessedMessages.Add(
            ProcessedMessage.Create(
                integrationEvent.EventId,
                ConsumerNames.StockReleaseRequested,
                now));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
