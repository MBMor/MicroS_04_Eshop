namespace Messaging.Shared.Outbox;

public sealed record ClaimedOutboxMessage(
    Guid Id,
    Guid EventId,
    Guid CorrelationId,
    string EventType,
    string RoutingKey,
    string Payload,
    string? TraceParent,
    string? TraceState);
