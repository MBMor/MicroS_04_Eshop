namespace Messaging.Shared.RabbitMq;

public static class RabbitMqQueues
{
    public const string InventoryOrderCreatedV1 = "inventory.order-created.v1";

    public const string InventoryStockReleaseRequestedV1 = "inventory.stock-release-requested.v1";

    public const string OrdersStockReservedV1 = "orders.stock-reserved.v1";

    public const string OrdersStockReservationFailedV1 = "orders.stock-reservation-failed.v1";

    public const string OrdersPaymentAuthorizedV1 = "orders.payment-authorized.v1";

    public const string OrdersPaymentFailedV1 = "orders.payment-failed.v1";

    public const string OrdersStockReleasedV1 = "orders.stock-released.v1";

    public const string PaymentsPaymentRequestedV1 = "payments.payment-requested.v1";

    public const string NotificationsOrderCreatedV1 = "notifications.order-created.v1";

    public const string NotificationsStockReservedV1 = "notifications.stock-reserved.v1";

    public const string NotificationsStockReservationFailedV1 = "notifications.stock-reservation-failed.v1";

    public const string NotificationsPaymentAuthorizedV1 = "notifications.payment-authorized.v1";

    public const string NotificationsPaymentFailedV1 = "notifications.payment-failed.v1";

    public const string NotificationsOrderConfirmedV1 = "notifications.order-confirmed.v1";

    public const string NotificationsOrderCancelledV1 = "notifications.order-cancelled.v1";

    public static string DeadLetter(string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        return $"{queueName}.dlq";
    }
}
