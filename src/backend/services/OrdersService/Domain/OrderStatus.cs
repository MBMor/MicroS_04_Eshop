namespace OrdersService.Domain;

public enum OrderStatus
{
    PendingStockReservation = 1,
    StockReservationFailed = 2,
    PendingPayment = 3,
    PaymentFailed = 4,
    Confirmed = 5,
    Cancelled = 6
}
