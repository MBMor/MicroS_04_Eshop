namespace OrdersService.Domain;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

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

        return order;
    }

    public void MarkStockReserved(DateTimeOffset updatedAtUtc)
    {
        if (Status != OrderStatus.PendingStockReservation)
        {
            throw new InvalidOperationException(
                $"Order in status '{Status}' cannot accept a stock reservation.");
        }

        Status = OrderStatus.PendingPayment;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void MarkStockReservationFailed(string reason, DateTimeOffset updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status != OrderStatus.PendingStockReservation)
        {
            throw new InvalidOperationException(
                $"Order in status '{Status}' cannot accept a failed stock reservation.");
        }

        Status = OrderStatus.StockReservationFailed;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void MarkPaymentAuthorized(DateTimeOffset updatedAtUtc)
    {
        Transition(
            OrderStatus.PendingPayment,
            OrderStatus.Confirmed,
            updatedAtUtc);
    }

    public void MarkPaymentFailed(DateTimeOffset updatedAtUtc)
    {
        Transition(
            OrderStatus.PendingPayment,
            OrderStatus.PaymentFailed,
            updatedAtUtc);
    }

    public void Cancel(DateTimeOffset updatedAtUtc)
    {
        if (Status is not (
            OrderStatus.PendingStockReservation
            or OrderStatus.PendingPayment
            or OrderStatus.PaymentFailed))
        {
            throw new InvalidOperationException(
                $"Order in status '{Status}' cannot be cancelled.");
        }

        Status = OrderStatus.Cancelled;
        UpdatedAtUtc = updatedAtUtc;
    }

    private void Transition(
        OrderStatus expectedStatus,
        OrderStatus newStatus,
        DateTimeOffset updatedAtUtc)
    {
        if (Status != expectedStatus)
        {
            throw new InvalidOperationException(
                $"Order status transition from '{Status}' to '{newStatus}' is not allowed.");
        }

        Status = newStatus;
        UpdatedAtUtc = updatedAtUtc;
    }
}
