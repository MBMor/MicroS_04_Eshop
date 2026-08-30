namespace Eshop.Operations.Desktop.Api.Orders;

public sealed record OperationalOrderDetailDto(
    Guid Id,
    string CustomerId,
    string CustomerEmail,
    string Status,
    decimal TotalAmount,
    string Currency,
    string PaymentMethod,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<OperationalOrderItemDto> Items,
    IReadOnlyList<OperationalOrderStatusHistoryDto> StatusHistory);
