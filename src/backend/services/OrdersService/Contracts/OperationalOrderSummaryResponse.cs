using OrdersService.Domain;

namespace OrdersService.Contracts;

public sealed record OperationalOrderSummaryResponse(
    Guid Id,
    string CustomerId,
    string CustomerEmail,
    string Status,
    decimal TotalAmount,
    string Currency,
    int ItemCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc)
{
    public static OperationalOrderSummaryResponse FromOrder(
        Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        return new OperationalOrderSummaryResponse(
            order.Id,
            order.CustomerId,
            order.CustomerEmail,
            order.Status.ToString(),
            order.TotalAmount,
            order.Currency,
            order.Items.Sum(item => item.Quantity),
            order.CreatedAtUtc,
            order.UpdatedAtUtc);
    }
}
