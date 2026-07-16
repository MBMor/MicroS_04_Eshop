namespace Eshop.Contracts.IntegrationEvents;

public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAtUtc { get; }

    Guid CorrelationId { get; }
}
