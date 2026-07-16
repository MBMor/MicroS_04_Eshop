namespace Eshop.Contracts.IntegrationEvents.V1;

public sealed record PaymentRequestedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    Guid OrderId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string PaymentMethod)
    : IIntegrationEvent;
