namespace Eshop.Contracts.IntegrationEvents.V1;

public sealed record StockReleasedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    Guid OrderId,
    string CustomerId,
    IReadOnlyList<ReleasedStockItemV1> Items)
    : IIntegrationEvent;

public sealed record ReleasedStockItemV1(
    Guid ProductId,
    string Sku,
    int Quantity);
