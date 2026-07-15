namespace OrdersService.Integration;

public sealed record BasketSnapshot(
    BasketItemSnapshot[] Items);

public sealed record BasketItemSnapshot(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    decimal LineTotal);
