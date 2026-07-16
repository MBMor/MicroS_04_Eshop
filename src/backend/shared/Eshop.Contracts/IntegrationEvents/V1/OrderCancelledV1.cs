namespace Eshop.Contracts.IntegrationEvents.V1;

public sealed record OrderCancelledV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    Guid OrderId,
    string CustomerId,
    string Reason)
    : IIntegrationEvent;
