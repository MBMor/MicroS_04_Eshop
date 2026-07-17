using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.RabbitMq;
using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Domain;
using OrdersService.Outbox;

namespace OrdersService.Application;

public sealed class OrderStockResultService(
    OrdersDbContext dbContext,
    OrdersOutboxWriter outboxWriter,
    TimeProvider timeProvider)
{
    public async Task ApplyStockReservedAsync(
        Guid orderId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        Order order = await dbContext.Orders
            .FirstOrDefaultAsync(
                candidate => candidate.Id == orderId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Order '{orderId}' does not exist.");

        DateTimeOffset now = timeProvider.GetUtcNow();

        order.MarkStockReserved(now);

        PaymentRequestedV1 paymentRequested = new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: now,
            CorrelationId: correlationId,
            OrderId: order.Id,
            CustomerId: order.CustomerId,
            Amount: order.TotalAmount,
            Currency: order.Currency,
            PaymentMethod: order.PaymentMethod);

        dbContext.OutboxMessages.Add(
            outboxWriter.Create(
                paymentRequested,
                RabbitMqRoutingKeys.PaymentRequestedV1));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyStockReservationFailedAsync(
        Guid orderId,
        string reason,
        CancellationToken cancellationToken)
    {
        Order order = await dbContext.Orders
            .FirstOrDefaultAsync(
                candidate => candidate.Id == orderId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Order '{orderId}' does not exist.");

        order.MarkStockReservationFailed(
            reason,
            timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
