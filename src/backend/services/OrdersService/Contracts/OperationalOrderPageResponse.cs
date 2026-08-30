namespace OrdersService.Contracts;

public sealed record OperationalOrderPageResponse(
    IReadOnlyList<OperationalOrderSummaryResponse> Items,
    int Offset,
    int Limit,
    bool HasMore);
