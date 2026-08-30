namespace Eshop.Operations.Desktop.Api.Orders;

public sealed record OperationalOrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    decimal LineTotal);
