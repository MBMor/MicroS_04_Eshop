namespace Messaging.Shared.Contracts;

public sealed record MessageEnvelope<TMessage>(
    Guid MessageId,
    string MessageType,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    string? TraceParent,
    string? TraceState,
    TMessage Payload);
