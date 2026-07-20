namespace OrdersService.Domain;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusHistory> _statusHistory = [];

    private Order()
    {
    }

    public Guid Id { get; private set; }

    public string CustomerId { get; private set; } = string.Empty;

    public string CustomerEmail { get; private set; } = string.Empty;

    public OrderStatus Status { get; private set; }

    public decimal TotalAmount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string PaymentMethod { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items;

    public IReadOnlyCollection<OrderStatusHistory> StatusHistory => _statusHistory;

    public static Order Create(
        Guid id,
        string customerId,
        string customerEmail,
        string paymentMethod,
        IEnumerable<OrderItem> items,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Order id must not be empty.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException(
                "Customer id must not be empty.",
                nameof(customerId));
        }

        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            throw new ArgumentException(
                "Customer email must not be empty.",
                nameof(customerEmail));
        }

        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            throw new ArgumentException(
                "Payment method must not be empty.",
                nameof(paymentMethod));
        }

        ArgumentNullException.ThrowIfNull(items);

        List<OrderItem> orderItems = items.ToList();

        if (orderItems.Count == 0)
        {
            throw new ArgumentException(
                "Order must contain at least one item.",
                nameof(items));
        }

        if (orderItems.Any(item => item.OrderId != id))
        {
            throw new ArgumentException(
                "All order items must belong to the created order.",
                nameof(items));
        }

        string[] currencies = orderItems
            .Select(item => item.Currency)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (currencies.Length != 1)
        {
            throw new ArgumentException(
                "All order items must use the same currency.",
                nameof(items));
        }

        decimal totalAmount = orderItems.Sum(item => item.LineTotal);

        if (totalAmount <= 0)
        {
            throw new ArgumentException(
                "Order total must be greater than zero.",
                nameof(items));
        }

        Order order = new()
        {
            Id = id,
            CustomerId = customerId.Trim(),
            CustomerEmail = customerEmail.Trim(),
            PaymentMethod = paymentMethod.Trim(),
            Status = OrderStatus.PendingStockReservation,
            TotalAmount = totalAmount,
            Currency = currencies[0].ToUpperInvariant(),
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = null
        };

        order._items.AddRange(orderItems);

        order._statusHistory.Add(
            OrderStatusHistory.Create(
                Guid.NewGuid(),
                order.Id,
                fromStatus: null,
                OrderStatus.PendingStockReservation,
                "Order created and awaiting stock reservation.",
                createdAtUtc));

        return order;
    }

    public void MarkStockReserved(
        DateTimeOffset updatedAtUtc)
    {
        Transition(
            OrderStatus.PendingStockReservation,
            OrderStatus.PendingPayment,
            "Stock reserved; order is awaiting payment.",
            updatedAtUtc);
    }

    public void MarkStockReservationFailed(
        string reason,
        DateTimeOffset updatedAtUtc)
    {
        Transition(
            OrderStatus.PendingStockReservation,
            OrderStatus.StockReservationFailed,
            reason,
            updatedAtUtc);
    }

    public void MarkPaymentAuthorized(
        DateTimeOffset updatedAtUtc)
    {
        Transition(
            OrderStatus.PendingPayment,
            OrderStatus.Confirmed,
            "Payment authorized.",
            updatedAtUtc);
    }

    public void MarkPaymentFailed(
        string reason,
        DateTimeOffset updatedAtUtc)
    {
        Transition(
            OrderStatus.PendingPayment,
            OrderStatus.PaymentFailed,
            reason,
            updatedAtUtc);
    }

    public void Cancel(
        string reason,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status is not (
            OrderStatus.PendingStockReservation
            or OrderStatus.PendingPayment
            or OrderStatus.PaymentFailed))
        {
            throw new InvalidOperationException(
                $"Order in status '{Status}' cannot be cancelled.");
        }

        ApplyTransition(
            OrderStatus.Cancelled,
            reason,
            updatedAtUtc);
    }

    private void Transition(
        OrderStatus expectedStatus,
        OrderStatus newStatus,
        string reason,
        DateTimeOffset updatedAtUtc)
    {
        if (Status != expectedStatus)
        {
            throw new InvalidOperationException(
                $"Order status transition from '{Status}' " +
                $"to '{newStatus}' is not allowed.");
        }

        ApplyTransition(
            newStatus,
            reason,
            updatedAtUtc);
    }

    private void ApplyTransition(
        OrderStatus newStatus,
        string reason,
        DateTimeOffset updatedAtUtc)
    {
        OrderStatus previousStatus =
            Status;

        OrderStatusHistory historyEntry =
            OrderStatusHistory.Create(
                Guid.NewGuid(),
                Id,
                previousStatus,
                newStatus,
                reason,
                updatedAtUtc);

        Status = newStatus;
        UpdatedAtUtc = updatedAtUtc;

        _statusHistory.Add(historyEntry);
    }
}
