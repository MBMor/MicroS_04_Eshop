namespace Messaging.Shared.RabbitMq;

public static class RabbitMqRoutingKeys
{
    public const string OrderCreatedV1 = "order.created.v1";

    public const string StockReservedV1 = "stock.reserved.v1";

    public const string StockReservationFailedV1 = "stock.reservation-failed.v1";

    public const string PaymentRequestedV1 = "payment.requested.v1";

    public const string PaymentAuthorizedV1 = "payment.authorized.v1";

    public const string PaymentFailedV1 = "payment.failed.v1";

    public const string StockReleaseRequestedV1 = "stock.release-requested.v1";

    public const string StockReleasedV1 = "stock.released.v1";

    public const string OrderConfirmedV1 = "order.confirmed.v1";

    public const string OrderCancelledV1 = "order.cancelled.v1";
}
