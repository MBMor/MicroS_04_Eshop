namespace Messaging.Shared.RabbitMq;

public static class RabbitMqHeaders
{
    public const string MessageId = "message-id";

    public const string MessageType = "message-type";

    public const string CorrelationId = "correlation-id";

    public const string OccurredAtUtc = "occurred-at-utc";

    public const string TraceParent = "traceparent";

    public const string TraceState = "tracestate";
}
