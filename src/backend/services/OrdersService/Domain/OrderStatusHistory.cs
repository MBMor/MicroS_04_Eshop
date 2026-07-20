namespace OrdersService.Domain;

public sealed class OrderStatusHistory
{
    internal const int MaximumReasonLength = 1_000;

    private OrderStatusHistory()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public OrderStatus? FromStatus { get; private set; }

    public OrderStatus ToStatus { get; private set; }

    public string Reason { get; private set; } =
        string.Empty;

    public DateTimeOffset ChangedAtUtc { get; private set; }

    internal static OrderStatusHistory Create(
        Guid id,
        Guid orderId,
        OrderStatus? fromStatus,
        OrderStatus toStatus,
        string reason,
        DateTimeOffset changedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Order status history id must not be empty.",
                nameof(id));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Order id must not be empty.",
                nameof(orderId));
        }

        if (fromStatus.HasValue
            && !Enum.IsDefined(fromStatus.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(fromStatus));
        }

        if (!Enum.IsDefined(toStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(toStatus));
        }

        string normalizedReason =
            NormalizeReason(reason);

        return new OrderStatusHistory
        {
            Id = id,
            OrderId = orderId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Reason = normalizedReason,
            ChangedAtUtc = changedAtUtc
        };
    }

    private static string NormalizeReason(
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        string normalizedReason =
            reason.Trim();

        if (normalizedReason.Length
            > MaximumReasonLength)
        {
            throw new ArgumentException(
                $"Status transition reason must not exceed " +
                $"{MaximumReasonLength} characters.",
                nameof(reason));
        }

        return normalizedReason;
    }
}
