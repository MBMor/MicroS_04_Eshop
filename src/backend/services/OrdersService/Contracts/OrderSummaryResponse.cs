using OrdersService.Domain;

namespace OrdersService.Contracts;

public sealed record OrderSummaryResponse(
    Guid Id,
    string Status,
    decimal TotalAmount,
    string Currency,
    int ItemCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc)
{
    public static OrderSummaryResponse FromOrder(Order order)
    {
        return new OrderSummaryResponse(
            order.Id,
            order.Status.ToString(),
            order.TotalAmount,
            order.Currency,
            order.Items.Sum(item => item.Quantity),
            order.CreatedAtUtc,
            order.UpdatedAtUtc);
    }
}
