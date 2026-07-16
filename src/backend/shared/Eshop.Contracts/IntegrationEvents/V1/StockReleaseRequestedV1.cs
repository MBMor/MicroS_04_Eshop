namespace Eshop.Contracts.IntegrationEvents.V1;

public sealed record StockReleaseRequestedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    Guid OrderId,
    string CustomerId,
    string Reason,
    IReadOnlyList<StockReleaseItemV1> Items)
    : IIntegrationEvent;

public sealed record StockReleaseItemV1(
    Guid ProductId,
    string Sku,
    int Quantity);
