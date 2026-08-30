namespace Eshop.Operations.Desktop.Api.Orders;

public sealed record OperationalOrderStatusHistoryDto(
    string? FromStatus,
    string ToStatus,
    string Reason,
    DateTimeOffset ChangedAtUtc);
