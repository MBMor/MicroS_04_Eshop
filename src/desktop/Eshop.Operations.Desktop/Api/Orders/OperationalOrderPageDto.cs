namespace Eshop.Operations.Desktop.Api.Orders;

public sealed record OperationalOrderPageDto(
    IReadOnlyList<OperationalOrderSummaryDto> Items,
    int Offset,
    int Limit,
    bool HasMore);
