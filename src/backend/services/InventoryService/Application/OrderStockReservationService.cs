using Eshop.Contracts.IntegrationEvents.V1;
using InventoryService.Data;
using InventoryService.Domain;
using InventoryService.Outbox;
using Messaging.Shared.RabbitMq;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Application;

public sealed class OrderStockReservationService(
    InventoryDbContext dbContext,
    InventoryOutboxWriter outboxWriter,
    TimeProvider timeProvider)
{
    public async Task<ReserveOrderStockResult> ReserveAsync(
        OrderCreatedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        Guid[] productIds = integrationEvent.Items
            .Select(item => item.ProductId)
            .Distinct()
            .ToArray();

        List<InventoryItem> inventoryItems = await dbContext.InventoryItems
            .Where(item => productIds.Contains(item.ProductId))
            .ToListAsync(cancellationToken);

        Dictionary<Guid, InventoryItem> inventoryByProductId = inventoryItems
            .ToDictionary(item => item.ProductId);

        List<StockReservationFailureItemV1> failures = [];

        foreach (OrderCreatedItemV1 requestedItem in integrationEvent.Items)
        {
            if (!inventoryByProductId.TryGetValue(requestedItem.ProductId, out InventoryItem? inventoryItem))
            {
                failures.Add(new StockReservationFailureItemV1(
                    requestedItem.ProductId,
                    requestedItem.Quantity,
                    0,
                    "Inventory item does not exist."));

                continue;
            }

            if (!inventoryItem.IsActive)
            {
                failures.Add(new StockReservationFailureItemV1(
                    requestedItem.ProductId,
                    requestedItem.Quantity,
                    inventoryItem.AvailableQuantity,
                    "Inventory item is inactive."));

                continue;
            }

            if (inventoryItem.AvailableQuantity < requestedItem.Quantity)
            {
                failures.Add(new StockReservationFailureItemV1(
                    requestedItem.ProductId,
                    requestedItem.Quantity,
                    inventoryItem.AvailableQuantity,
                    "Insufficient available stock."));
            }
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (failures.Count > 0)
        {
            StockReservationFailedV1 failedEvent = new(
                Guid.NewGuid(),
                now,
                integrationEvent.CorrelationId,
                integrationEvent.OrderId,
                integrationEvent.CustomerId,
                "One or more order items could not be reserved.",
                failures);

            dbContext.OutboxMessages.Add(
                outboxWriter.Create(
                    failedEvent,
                    RabbitMqRoutingKeys.StockReservationFailedV1));

            await dbContext.SaveChangesAsync(cancellationToken);

            return new ReserveOrderStockResult(
                ReserveOrderStockStatus.Failed,
                failedEvent.Reason);
        }

        foreach (OrderCreatedItemV1 requestedItem in integrationEvent.Items)
        {
            InventoryItem inventoryItem = inventoryByProductId[requestedItem.ProductId];

            bool reserved = inventoryItem.TryReserve(
                requestedItem.Quantity,
                now);

            if (!reserved)
            {
                throw new InvalidOperationException(
                    $"Inventory item '{inventoryItem.Id}' passed validation but could not be reserved.");
            }
        }

        StockReservedV1 reservedEvent = new(
            Guid.NewGuid(),
            now,
            integrationEvent.CorrelationId,
            integrationEvent.OrderId,
            integrationEvent.CustomerId,
            integrationEvent.Items
                .Select(item => new ReservedStockItemV1(item.ProductId, item.Quantity))
                .ToArray());

        dbContext.OutboxMessages.Add(
            outboxWriter.Create(
                reservedEvent,
                RabbitMqRoutingKeys.StockReservedV1));

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ReserveOrderStockResult(
            ReserveOrderStockStatus.Reserved,
            null);
    }
}
