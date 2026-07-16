namespace Eshop.Contracts.IntegrationEvents.V1;

public sealed record PaymentFailedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    Guid OrderId,
    Guid PaymentId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string Reason)
    : IIntegrationEvent;
