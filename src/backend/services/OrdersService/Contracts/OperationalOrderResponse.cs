using OrdersService.Domain;

namespace OrdersService.Contracts;

public sealed record OperationalOrderResponse(
    Guid Id,
    string CustomerId,
    string CustomerEmail,
    string Status,
    decimal TotalAmount,
    string Currency,
    string PaymentMethod,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    OrderItemResponse[] Items,
    OrderStatusHistoryResponse[] StatusHistory)
{
    public static OperationalOrderResponse FromOrder(
        Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        OrderResponse customerResponse =
            OrderResponse.FromOrder(order);

        return new OperationalOrderResponse(
            customerResponse.Id,
            order.CustomerId,
            customerResponse.CustomerEmail,
            customerResponse.Status,
            customerResponse.TotalAmount,
            customerResponse.Currency,
            customerResponse.PaymentMethod,
            customerResponse.CreatedAtUtc,
            customerResponse.UpdatedAtUtc,
            customerResponse.Items,
            customerResponse.StatusHistory);
    }
}
