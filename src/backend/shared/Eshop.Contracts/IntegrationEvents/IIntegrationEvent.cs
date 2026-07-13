namespace Eshop.Contracts.IntegrationEvents;

public interface IIntegrationEvent
{
    Guid EventId { get; }

    string EventType { get; }

    DateTimeOffset OccurredAtUtc { get; }

    Guid CorrelationId { get; }
}
