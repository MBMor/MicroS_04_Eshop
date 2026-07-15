namespace OrdersService.Domain;

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    public static OrderItem Create(
        Guid id,
        Guid orderId,
        Guid productId,
        string productName,
        decimal unitPrice,
        string currency,
        int quantity)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Order item id must not be empty.",
                nameof(id));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Order id must not be empty.",
                nameof(orderId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException(
                "Product id must not be empty.",
                nameof(productId));
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ArgumentException(
                "Product name must not be empty.",
                nameof(productName));
        }

        if (unitPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice),
                unitPrice,
                "Unit price must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException(
                "Currency must not be empty.",
                nameof(currency));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "Quantity must be greater than zero.");
        }

        return new OrderItem
        {
            Id = id,
            OrderId = orderId,
            ProductId = productId,
            ProductName = productName.Trim(),
            UnitPrice = unitPrice,
            Currency = currency.Trim().ToUpperInvariant(),
            Quantity = quantity
        };
    }
}
