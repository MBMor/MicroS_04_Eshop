namespace Messaging.Shared.RabbitMq;

public static class RabbitMqTopology
{
    public static IReadOnlyList<RabbitMqBindingDefinition> Bindings { get; } =
    [
        new(
            RabbitMqQueues.InventoryOrderCreatedV1,
            RabbitMqRoutingKeys.OrderCreatedV1),

        new(
            RabbitMqQueues.InventoryStockReleaseRequestedV1,
            RabbitMqRoutingKeys.StockReleaseRequestedV1),

        new(
            RabbitMqQueues.OrdersStockReservedV1,
            RabbitMqRoutingKeys.StockReservedV1),

        new(
            RabbitMqQueues.OrdersStockReservationFailedV1,
            RabbitMqRoutingKeys.StockReservationFailedV1),

        new(
            RabbitMqQueues.OrdersPaymentAuthorizedV1,
            RabbitMqRoutingKeys.PaymentAuthorizedV1),

        new(
            RabbitMqQueues.OrdersPaymentFailedV1,
            RabbitMqRoutingKeys.PaymentFailedV1),

        new(
            RabbitMqQueues.OrdersStockReleasedV1,
            RabbitMqRoutingKeys.StockReleasedV1),

        new(
            RabbitMqQueues.PaymentsPaymentRequestedV1,
            RabbitMqRoutingKeys.PaymentRequestedV1),

        new(
            RabbitMqQueues.NotificationsOrderCreatedV1,
            RabbitMqRoutingKeys.OrderCreatedV1),

        new(
            RabbitMqQueues.NotificationsStockReservedV1,
            RabbitMqRoutingKeys.StockReservedV1),

        new(
            RabbitMqQueues.NotificationsStockReservationFailedV1,
            RabbitMqRoutingKeys.StockReservationFailedV1),

        new(
            RabbitMqQueues.NotificationsPaymentAuthorizedV1,
            RabbitMqRoutingKeys.PaymentAuthorizedV1),

        new(
            RabbitMqQueues.NotificationsPaymentFailedV1,
            RabbitMqRoutingKeys.PaymentFailedV1),

        new(
            RabbitMqQueues.NotificationsOrderConfirmedV1,
            RabbitMqRoutingKeys.OrderConfirmedV1),

        new(
            RabbitMqQueues.NotificationsOrderCancelledV1,
            RabbitMqRoutingKeys.OrderCancelledV1)
    ];
}
