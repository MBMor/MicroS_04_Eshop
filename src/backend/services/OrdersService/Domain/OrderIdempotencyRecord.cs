namespace OrdersService.Domain;

public enum OrderIdempotencyStatus
{
    Completed
}

public sealed class OrderIdempotencyRecord
{
    public const string CreateOrderOperation =
        "CreateOrder";

    public const string UniqueCommandIndexName =
        "ux_order_idempotency_customer_operation_key";

    private OrderIdempotencyRecord()
    {
    }

    public Guid Id { get; private set; }

    public string CustomerId { get; private set; } =
        string.Empty;

    public string Operation { get; private set; } =
        string.Empty;

    public string IdempotencyKey { get; private set; } =
        string.Empty;

    public string RequestFingerprint { get; private set; } =
        string.Empty;

    public Guid OrderId { get; private set; }

    public OrderIdempotencyStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static OrderIdempotencyRecord CreateCompleted(
        Guid id,
        string customerId,
        string idempotencyKey,
        string requestFingerprint,
        Guid orderId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Idempotency record id must not be empty.",
                nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Order id must not be empty.",
                nameof(orderId));
        }

        return new OrderIdempotencyRecord
        {
            Id = id,
            CustomerId = customerId,
            Operation = CreateOrderOperation,
            IdempotencyKey = idempotencyKey,
            RequestFingerprint = requestFingerprint,
            OrderId = orderId,
            Status = OrderIdempotencyStatus.Completed,
            CreatedAtUtc = createdAtUtc
        };
    }
}
