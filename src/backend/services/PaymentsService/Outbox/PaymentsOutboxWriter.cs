using System.Diagnostics;
using System.Text.Json;
using Eshop.Contracts.IntegrationEvents;

namespace PaymentsService.Outbox;

public sealed class PaymentsOutboxWriter
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

    public OutboxMessage Create<TEvent>(
        TEvent integrationEvent,
        string routingKey)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        string payload = JsonSerializer.Serialize(
            integrationEvent,
            SerializerOptions);

        return OutboxMessage.Create(
            id: Guid.NewGuid(),
            eventId: integrationEvent.EventId,
            eventType: typeof(TEvent).FullName ?? typeof(TEvent).Name,
            routingKey: routingKey,
            payload: payload,
            occurredAtUtc: integrationEvent.OccurredAtUtc,
            correlationId: integrationEvent.CorrelationId,
            traceParent: Activity.Current?.Id,
            traceState: Activity.Current?.TraceStateString);
    }
}
