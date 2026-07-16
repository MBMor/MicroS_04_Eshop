namespace Eshop.Contracts.IntegrationEvents.V1;

public sealed record OrderCreatedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderCreatedItemV1> Items)
    : IIntegrationEvent;

public sealed record OrderCreatedItemV1(
    Guid ProductId,
    string ProductName,
    string Sku,
    int Quantity,
    decimal UnitPrice);
