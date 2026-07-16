namespace NotificationsService.Domain;

public enum NotificationType
{
    OrderCreated = 1,
    StockReserved = 2,
    StockReservationFailed = 3,
    PaymentAuthorized = 4,
    PaymentFailed = 5,
    OrderConfirmed = 6,
    OrderCancelled = 7
}
