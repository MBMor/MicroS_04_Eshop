namespace Eshop.Contracts.IntegrationEvents.V1;

public sealed record StockReservationFailedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    Guid OrderId,
    string CustomerId,
    string Reason,
    IReadOnlyList<StockReservationFailureItemV1> FailedItems)
    : IIntegrationEvent;

public sealed record StockReservationFailureItemV1(
    Guid ProductId,
    string Sku,
    int RequestedQuantity,
    int AvailableQuantity,
    string Reason);
