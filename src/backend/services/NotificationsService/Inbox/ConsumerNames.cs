namespace NotificationsService.Inbox;

public static class ConsumerNames
{
    public const string NotificationsOrderCreated = "notifications.order-created.v1";
    public const string NotificationsStockReserved = "notifications.stock-reserved.v1";
    public const string NotificationsStockReservationFailed = "notifications.stock-reservation-failed.v1";
    public const string NotificationsPaymentAuthorized = "notifications.payment-authorized.v1";
    public const string NotificationsPaymentFailed = "notifications.payment-failed.v1";
    public const string NotificationsOrderConfirmed = "notifications.order-confirmed.v1";
    public const string NotificationsOrderCancelled = "notifications.order-cancelled.v1";
}
