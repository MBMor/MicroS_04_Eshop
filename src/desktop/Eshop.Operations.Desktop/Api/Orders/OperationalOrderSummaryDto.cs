namespace Eshop.Operations.Desktop.Api.Orders;

public sealed record OperationalOrderSummaryDto(
    Guid Id,
    string CustomerId,
    string CustomerEmail,
    string Status,
    decimal TotalAmount,
    string Currency,
    int ItemCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
