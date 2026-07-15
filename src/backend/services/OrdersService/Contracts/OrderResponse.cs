using OrdersService.Domain;

namespace OrdersService.Contracts;

public sealed record OrderResponse(
    Guid Id,
    string CustomerEmail,
    string Status,
    decimal TotalAmount,
    string Currency,
    string PaymentMethod,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    OrderItemResponse[] Items)
{
    public static OrderResponse FromOrder(Order order)
    {
        OrderItemResponse[] items = order.Items
            .OrderBy(item => item.ProductName)
            .Select(item => new OrderItemResponse(
                item.Id,
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Currency,
                item.Quantity,
                item.LineTotal))
            .ToArray();

        return new OrderResponse(
            order.Id,
            order.CustomerEmail,
            order.Status.ToString(),
            order.TotalAmount,
            order.Currency,
            order.PaymentMethod,
            order.CreatedAtUtc,
            order.UpdatedAtUtc,
            items);
    }
}

public sealed record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    decimal LineTotal);
