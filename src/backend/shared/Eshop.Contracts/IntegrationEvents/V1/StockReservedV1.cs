namespace Eshop.Contracts.IntegrationEvents.V1;

public sealed record StockReservedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    Guid OrderId,
    string CustomerId,
    IReadOnlyList<ReservedStockItemV1> Items)
    : IIntegrationEvent;

public sealed record ReservedStockItemV1(
    Guid ProductId,
    string Sku,
    int Quantity);
