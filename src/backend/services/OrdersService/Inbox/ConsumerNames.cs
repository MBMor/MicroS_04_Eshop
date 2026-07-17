namespace OrdersService.Inbox;

public static class ConsumerNames
{
    public const string StockReserved = "orders.stock-reserved.v1";
    public const string StockReservationFailed = "orders.stock-reservation-failed.v1";
    public const string PaymentAuthorized = "orders.payment-authorized.v1";
    public const string PaymentFailed = "orders.payment-failed.v1";
    public const string StockReleased = "orders.stock-released.v1";
}
