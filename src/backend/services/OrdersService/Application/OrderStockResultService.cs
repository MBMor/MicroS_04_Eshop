using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.RabbitMq;
using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Domain;
using OrdersService.Inbox;
using OrdersService.Outbox;

namespace OrdersService.Application;

public sealed class OrderStockResultService(
    OrdersDbContext dbContext,
    OrdersOutboxWriter outboxWriter,
    TimeProvider timeProvider)
    : IOrderStockResultService
{
    public async Task ApplyStockReservedAsync(
        StockReservedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        bool alreadyProcessed = await dbContext.ProcessedMessages
            .AnyAsync(
                message =>
                    message.EventId == integrationEvent.EventId
                    && message.ConsumerName == ConsumerNames.StockReserved,
                cancellationToken);

        if (alreadyProcessed)
        {
            return;
        }

        Order order = await dbContext.Orders
            .FirstOrDefaultAsync(
                candidate => candidate.Id == integrationEvent.OrderId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Order '{integrationEvent.OrderId}' does not exist.");

        DateTimeOffset now = timeProvider.GetUtcNow();

        order.MarkStockReserved(now);

        PaymentRequestedV1 paymentRequested = new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: now,
            CorrelationId: integrationEvent.CorrelationId,
            OrderId: order.Id,
            CustomerId: order.CustomerId,
            Amount: order.TotalAmount,
            Currency: order.Currency,
            PaymentMethod: order.PaymentMethod);

        dbContext.OutboxMessages.Add(
            outboxWriter.Create(
                paymentRequested,
                RabbitMqRoutingKeys.PaymentRequestedV1));

        dbContext.ProcessedMessages.Add(
            ProcessedMessage.Create(
                integrationEvent.EventId,
                ConsumerNames.StockReserved,
                now));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyStockReservationFailedAsync(
        StockReservationFailedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        bool alreadyProcessed = await dbContext.ProcessedMessages
            .AnyAsync(
                message =>
                    message.EventId == integrationEvent.EventId
                    && message.ConsumerName == ConsumerNames.StockReservationFailed,
                cancellationToken);

        if (alreadyProcessed)
        {
            return;
        }

        Order order = await dbContext.Orders
            .FirstOrDefaultAsync(
                candidate => candidate.Id == integrationEvent.OrderId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Order '{integrationEvent.OrderId}' does not exist.");

        DateTimeOffset now = timeProvider.GetUtcNow();

        order.MarkStockReservationFailed(
            integrationEvent.Reason,
            now);

        dbContext.ProcessedMessages.Add(
            ProcessedMessage.Create(
                integrationEvent.EventId,
                ConsumerNames.StockReservationFailed,
                now));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
