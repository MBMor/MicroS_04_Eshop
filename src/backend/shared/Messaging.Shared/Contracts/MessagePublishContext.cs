namespace Messaging.Shared.Contracts;

public sealed record MessagePublishContext(
    string? TraceParent = null,
    string? TraceState = null);
