namespace Messaging.Shared.Contracts;

public sealed record MessagePublishContext(
    Guid CorrelationId,
    string? TraceParent = null,
    string? TraceState = null);
