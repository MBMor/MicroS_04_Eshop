namespace Messaging.Shared.Contracts;

public sealed record MessageContext(
    Guid MessageId,
    string MessageType,
    Guid CorrelationId,
    string RoutingKey,
    string? TraceParent,
    string? TraceState,
    ulong DeliveryTag,
    bool Redelivered);
