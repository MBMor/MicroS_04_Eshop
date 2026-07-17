using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.RabbitMq;
using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Domain;
using OrdersService.Outbox;

namespace OrdersService.Application;

public sealed class OrderPaymentResultService(
    OrdersDbContext dbContext,
    OrdersOutboxWriter outboxWriter,
    TimeProvider timeProvider)
{
    public async Task ApplyPaymentAuthorizedAsync(
        PaymentAuthorizedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        Order order = await dbContext.Orders
            .Include(candidate => candidate.Items)
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.Id == integrationEvent.OrderId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Order '{integrationEvent.OrderId}' does not exist.");

        DateTimeOffset now = timeProvider.GetUtcNow();

        order.MarkPaymentAuthorized(now);

        OrderConfirmedV1 orderConfirmed = new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: now,
            CorrelationId: integrationEvent.CorrelationId,
            OrderId: order.Id,
            CustomerId: order.CustomerId,
            TotalAmount: order.TotalAmount,
            Currency: order.Currency);

        dbContext.OutboxMessages.Add(
            outboxWriter.Create(
                orderConfirmed,
                RabbitMqRoutingKeys.OrderConfirmedV1));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyPaymentFailedAsync(
        PaymentFailedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        Order order = await dbContext.Orders
            .Include(candidate => candidate.Items)
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.Id == integrationEvent.OrderId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Order '{integrationEvent.OrderId}' does not exist.");

        DateTimeOffset now = timeProvider.GetUtcNow();

        order.MarkPaymentFailed(now);

        StockReleaseRequestedV1 stockReleaseRequested = new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: now,
            CorrelationId: integrationEvent.CorrelationId,
            OrderId: order.Id,
            CustomerId: order.CustomerId,
            Reason: integrationEvent.Reason,
            Items: order.Items
                .Select(item => new StockReleaseItemV1(
                    item.ProductId,
                    item.Quantity))
                .ToArray());

        dbContext.OutboxMessages.Add(
            outboxWriter.Create(
                stockReleaseRequested,
                RabbitMqRoutingKeys.StockReleaseRequestedV1));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyStockReleasedAsync(
        StockReleasedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        Order order = await dbContext.Orders
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.Id == integrationEvent.OrderId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Order '{integrationEvent.OrderId}' does not exist.");

        DateTimeOffset now = timeProvider.GetUtcNow();

        order.Cancel(now);

        OrderCancelledV1 orderCancelled = new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: now,
            CorrelationId: integrationEvent.CorrelationId,
            OrderId: order.Id,
            CustomerId: order.CustomerId,
            Reason: "Payment failed and reserved stock was released.");

        dbContext.OutboxMessages.Add(
            outboxWriter.Create(
                orderCancelled,
                RabbitMqRoutingKeys.OrderCancelledV1));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
