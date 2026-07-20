using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.RabbitMq;
using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Domain;
using OrdersService.Inbox;
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
        ArgumentNullException.ThrowIfNull(integrationEvent);

        bool alreadyProcessed = await dbContext.ProcessedMessages
            .AnyAsync(
                message =>
                    message.EventId == integrationEvent.EventId
                    && message.ConsumerName == ConsumerNames.PaymentAuthorized,
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

        dbContext.ProcessedMessages.Add(
            ProcessedMessage.Create(
                integrationEvent.EventId,
                ConsumerNames.PaymentAuthorized,
                now));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyPaymentFailedAsync(
        PaymentFailedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        bool alreadyProcessed = await dbContext.ProcessedMessages
            .AnyAsync(
                message =>
                    message.EventId == integrationEvent.EventId
                    && message.ConsumerName == ConsumerNames.PaymentFailed,
                cancellationToken);

        if (alreadyProcessed)
        {
            return;
        }

        Order order = await dbContext.Orders
            .Include(candidate => candidate.Items)
            .FirstOrDefaultAsync(
                candidate => candidate.Id == integrationEvent.OrderId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Order '{integrationEvent.OrderId}' does not exist.");

        DateTimeOffset now = timeProvider.GetUtcNow();

        order.MarkPaymentFailed(integrationEvent.Reason, now);

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

        dbContext.ProcessedMessages.Add(
            ProcessedMessage.Create(
                integrationEvent.EventId,
                ConsumerNames.PaymentFailed,
                now));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyStockReleasedAsync(
        StockReleasedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        bool alreadyProcessed = await dbContext.ProcessedMessages
            .AnyAsync(
                message =>
                    message.EventId == integrationEvent.EventId
                    && message.ConsumerName == ConsumerNames.StockReleased,
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

        DateTimeOffset now =
            timeProvider.GetUtcNow();

        const string cancellationReason =
            "Payment failed and reserved stock was released.";

        order.Cancel(
            cancellationReason,
            now);

        OrderCancelledV1 orderCancelled = new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: now,
            CorrelationId: integrationEvent.CorrelationId,
            OrderId: order.Id,
            CustomerId: order.CustomerId,
            Reason: cancellationReason);

        dbContext.OutboxMessages.Add(
            outboxWriter.Create(
                orderCancelled,
                RabbitMqRoutingKeys.OrderCancelledV1));

        dbContext.ProcessedMessages.Add(
            ProcessedMessage.Create(
                integrationEvent.EventId,
                ConsumerNames.StockReleased,
                now));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
