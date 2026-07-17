using Eshop.Contracts.IntegrationEvents;
using Eshop.Contracts.IntegrationEvents.V1;
using Microsoft.EntityFrameworkCore;
using NotificationsService.Data;
using NotificationsService.Domain;
using NotificationsService.Inbox;

namespace NotificationsService.Application;

public sealed class NotificationEventProcessingService(
    NotificationsDbContext dbContext,
    TimeProvider timeProvider)
{
    public Task ProcessAsync(
        OrderCreatedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        return CreateNotificationAsync(
            integrationEvent,
            NotificationType.OrderCreated,
            "Order created",
            $"Order {integrationEvent.OrderId} was created and is waiting for stock reservation.",
            ConsumerNames.NotificationsOrderCreated,
            integrationEvent.OrderId,
            integrationEvent.CustomerId,
            cancellationToken);
    }

    public Task ProcessAsync(
        StockReservedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        return CreateNotificationAsync(
            integrationEvent,
            NotificationType.StockReserved,
            "Stock reserved",
            $"Stock was successfully reserved for order {integrationEvent.OrderId}.",
            ConsumerNames.NotificationsStockReserved,
            integrationEvent.OrderId,
            integrationEvent.CustomerId,
            cancellationToken);
    }

    public Task ProcessAsync(
        StockReservationFailedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        return CreateNotificationAsync(
            integrationEvent,
            NotificationType.StockReservationFailed,
            "Stock reservation failed",
            $"Stock could not be reserved for order {integrationEvent.OrderId}: {integrationEvent.Reason}",
            ConsumerNames.NotificationsStockReservationFailed,
            integrationEvent.OrderId,
            integrationEvent.CustomerId,
            cancellationToken);
    }

    public Task ProcessAsync(
        PaymentAuthorizedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        return CreateNotificationAsync(
            integrationEvent,
            NotificationType.PaymentAuthorized,
            "Payment authorized",
            $"Payment for order {integrationEvent.OrderId} was authorized.",
            ConsumerNames.NotificationsPaymentAuthorized,
            integrationEvent.OrderId,
            integrationEvent.CustomerId,
            cancellationToken);
    }

    public Task ProcessAsync(
        PaymentFailedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        return CreateNotificationAsync(
            integrationEvent,
            NotificationType.PaymentFailed,
            "Payment failed",
            $"Payment for order {integrationEvent.OrderId} failed: {integrationEvent.Reason}",
            ConsumerNames.NotificationsPaymentFailed,
            integrationEvent.OrderId,
            integrationEvent.CustomerId,
            cancellationToken);
    }

    public Task ProcessAsync(
        OrderConfirmedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        return CreateNotificationAsync(
            integrationEvent,
            NotificationType.OrderConfirmed,
            "Order confirmed",
            $"Order {integrationEvent.OrderId} was confirmed.",
            ConsumerNames.NotificationsOrderConfirmed,
            integrationEvent.OrderId,
            integrationEvent.CustomerId,
            cancellationToken);
    }

    public Task ProcessAsync(
        OrderCancelledV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        return CreateNotificationAsync(
            integrationEvent,
            NotificationType.OrderCancelled,
            "Order cancelled",
            $"Order {integrationEvent.OrderId} was cancelled: {integrationEvent.Reason}",
            ConsumerNames.NotificationsOrderCancelled,
            integrationEvent.OrderId,
            integrationEvent.CustomerId,
            cancellationToken);
    }

    private async Task CreateNotificationAsync<TEvent>(
        TEvent integrationEvent,
        NotificationType notificationType,
        string title,
        string message,
        string consumerName,
        Guid orderId,
        string customerId,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        bool alreadyProcessed = await dbContext.ProcessedMessages
            .AnyAsync(
                processedMessage =>
                    processedMessage.EventId == integrationEvent.EventId
                    && processedMessage.ConsumerName == consumerName,
                cancellationToken);

        if (alreadyProcessed)
        {
            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        Notification notification = Notification.Create(
            id: Guid.NewGuid(),
            customerId: customerId,
            orderId: orderId,
            type: notificationType,
            title: title,
            message: message,
            createdAtUtc: now,
            sourceEventId: integrationEvent.EventId,
            correlationId: integrationEvent.CorrelationId);

        dbContext.Notifications.Add(notification);

        dbContext.ProcessedMessages.Add(
            ProcessedMessage.Create(
                integrationEvent.EventId,
                consumerName,
                now));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
