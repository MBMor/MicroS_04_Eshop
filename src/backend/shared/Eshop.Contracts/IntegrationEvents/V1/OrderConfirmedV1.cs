namespace Eshop.Contracts.IntegrationEvents.V1;

public sealed record OrderConfirmedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    string Currency)
    : IIntegrationEvent;
