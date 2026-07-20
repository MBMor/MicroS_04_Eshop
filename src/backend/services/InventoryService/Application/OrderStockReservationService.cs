using Eshop.Contracts.IntegrationEvents.V1;
using InventoryService.Data;
using InventoryService.Domain;
using InventoryService.Inbox;
using InventoryService.Outbox;
using Messaging.Shared.RabbitMq;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Application;

public sealed class OrderStockReservationService(
    InventoryDbContext dbContext,
    InventoryOutboxWriter outboxWriter,
    TimeProvider timeProvider)
{
    private const int MaximumConcurrencyAttempts = 3;

    public async Task<ReserveOrderStockResult> ReserveAsync(
        OrderCreatedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        for (
            int attempt = 1;
            attempt <= MaximumConcurrencyAttempts;
            attempt++)
        {
            try
            {
                return await ReserveCoreAsync(
                    integrationEvent,
                    cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
                when (attempt < MaximumConcurrencyAttempts)
            {
                // Odstraní entity a Added outbox/inbox záznamy
                // z neúspěšného pokusu.
                dbContext.ChangeTracker.Clear();
            }
        }

        throw new DbUpdateConcurrencyException(
            $"Inventory reservation for order " +
            $"'{integrationEvent.OrderId}' could not be completed " +
            $"after {MaximumConcurrencyAttempts} concurrency attempts.");
    }

    private async Task<ReserveOrderStockResult> ReserveCoreAsync(
        OrderCreatedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        bool alreadyProcessed =
            await dbContext.ProcessedMessages
                .AnyAsync(
                    message =>
                        message.EventId
                            == integrationEvent.EventId
                        && message.ConsumerName
                            == ConsumerNames.OrderCreated,
                    cancellationToken);

        if (alreadyProcessed)
        {
            return new ReserveOrderStockResult(
                ReserveOrderStockStatus.Reserved,
                null);
        }

        Guid[] productIds = integrationEvent.Items
            .Select(item => item.ProductId)
            .Distinct()
            .ToArray();

        List<InventoryItem> inventoryItems =
            await dbContext.InventoryItems
                .Where(item =>
                    productIds.Contains(item.ProductId))
                .ToListAsync(cancellationToken);

        Dictionary<Guid, InventoryItem> inventoryByProductId =
            inventoryItems.ToDictionary(
                item => item.ProductId);

        List<StockReservationFailureItemV1> failures = [];

        foreach (
            OrderCreatedItemV1 requestedItem
            in integrationEvent.Items)
        {
            if (!inventoryByProductId.TryGetValue(
                    requestedItem.ProductId,
                    out InventoryItem? inventoryItem))
            {
                failures.Add(
                    new StockReservationFailureItemV1(
                        ProductId:
                            requestedItem.ProductId,
                        RequestedQuantity:
                            requestedItem.Quantity,
                        AvailableQuantity: 0,
                        Reason:
                            "Inventory item does not exist."));

                continue;
            }

            if (!inventoryItem.IsActive)
            {
                failures.Add(
                    new StockReservationFailureItemV1(
                        ProductId:
                            requestedItem.ProductId,
                        RequestedQuantity:
                            requestedItem.Quantity,
                        AvailableQuantity:
                            inventoryItem.AvailableQuantity,
                        Reason:
                            "Inventory item is inactive."));

                continue;
            }

            if (inventoryItem.AvailableQuantity
                < requestedItem.Quantity)
            {
                failures.Add(
                    new StockReservationFailureItemV1(
                        ProductId:
                            requestedItem.ProductId,
                        RequestedQuantity:
                            requestedItem.Quantity,
                        AvailableQuantity:
                            inventoryItem.AvailableQuantity,
                        Reason:
                            "Insufficient available stock."));
            }
        }

        DateTimeOffset now =
            timeProvider.GetUtcNow();

        if (failures.Count > 0)
        {
            StockReservationFailedV1 stockReservationFailed =
                new(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: now,
                    CorrelationId:
                        integrationEvent.CorrelationId,
                    OrderId:
                        integrationEvent.OrderId,
                    CustomerId:
                        integrationEvent.CustomerId,
                    Reason:
                        "One or more order items could not be reserved.",
                    FailedItems: failures);

            dbContext.OutboxMessages.Add(
                outboxWriter.Create(
                    stockReservationFailed,
                    RabbitMqRoutingKeys
                        .StockReservationFailedV1));

            dbContext.ProcessedMessages.Add(
                ProcessedMessage.Create(
                    integrationEvent.EventId,
                    ConsumerNames.OrderCreated,
                    now));

            await dbContext.SaveChangesAsync(
                cancellationToken);

            return new ReserveOrderStockResult(
                ReserveOrderStockStatus.Failed,
                stockReservationFailed.Reason);
        }

        foreach (
            OrderCreatedItemV1 requestedItem
            in integrationEvent.Items)
        {
            InventoryItem inventoryItem =
                inventoryByProductId[
                    requestedItem.ProductId];

            bool reserved =
                inventoryItem.TryReserve(
                    requestedItem.Quantity,
                    now);

            if (!reserved)
            {
                throw new InvalidOperationException(
                    $"Inventory item '{inventoryItem.Id}' " +
                    "passed validation but could not be reserved.");
            }
        }

        StockReservedV1 stockReserved = new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: now,
            CorrelationId:
                integrationEvent.CorrelationId,
            OrderId:
                integrationEvent.OrderId,
            CustomerId:
                integrationEvent.CustomerId,
            Items: integrationEvent.Items
                .Select(item =>
                    new ReservedStockItemV1(
                        item.ProductId,
                        item.Quantity))
                .ToArray());

        dbContext.OutboxMessages.Add(
            outboxWriter.Create(
                stockReserved,
                RabbitMqRoutingKeys.StockReservedV1));

        dbContext.ProcessedMessages.Add(
            ProcessedMessage.Create(
                integrationEvent.EventId,
                ConsumerNames.OrderCreated,
                now));

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return new ReserveOrderStockResult(
            ReserveOrderStockStatus.Reserved,
            null);
    }
}
