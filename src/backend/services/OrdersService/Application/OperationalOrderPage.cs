using OrdersService.Domain;

namespace OrdersService.Application;

public sealed record OperationalOrderPage(
    IReadOnlyList<Order> Items,
    int Offset,
    int Limit,
    bool HasMore);
