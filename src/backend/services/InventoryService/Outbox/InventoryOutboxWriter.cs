using System.Diagnostics;
using System.Text.Json;
using Eshop.Contracts.IntegrationEvents;

namespace InventoryService.Outbox;

public sealed class InventoryOutboxWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public OutboxMessage Create<TEvent>(TEvent integrationEvent, string routingKey)
        where TEvent : IIntegrationEvent
    {
        string payload = JsonSerializer.Serialize(integrationEvent, SerializerOptions);

        return OutboxMessage.Create(
            Guid.NewGuid(),
            integrationEvent.EventId,
            typeof(TEvent).FullName ?? typeof(TEvent).Name,
            routingKey,
            payload,
            integrationEvent.OccurredAtUtc,
            integrationEvent.CorrelationId,
            Activity.Current?.Id,
            Activity.Current?.TraceStateString);
    }
}
