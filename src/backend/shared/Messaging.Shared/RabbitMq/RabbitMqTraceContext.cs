using System.Diagnostics;
using System.Text;
using Messaging.Shared.Contracts;
using RabbitMQ.Client;

namespace Messaging.Shared.RabbitMq;

public static class RabbitMqTraceContext
{
    public static ActivityContext ExtractParentContext(
        IReadOnlyDictionary<string, object?>? headers)
    {
        string? traceParent = ReadHeader(
            headers,
            RabbitMqHeaders.TraceParent);

        string? traceState = ReadHeader(
            headers,
            RabbitMqHeaders.TraceState);

        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return default;
        }

        return ActivityContext.TryParse(
            traceParent,
            traceState,
            out ActivityContext parentContext)
                ? parentContext
                : default;
    }

    public static Guid? ExtractCorrelationId(
        IReadOnlyDictionary<string, object?>? headers)
    {
        string? value = ReadHeader(
            headers,
            RabbitMqHeaders.CorrelationId);

        return Guid.TryParse(value, out Guid correlationId)
            ? correlationId
            : null;
    }

    public static void Inject(
        BasicProperties properties,
        MessagePublishContext context,
        Activity? activity)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(context);

        properties.Headers ??=
            new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);

        properties.Headers[RabbitMqHeaders.CorrelationId] =
            Encoding.UTF8.GetBytes(
                context.CorrelationId.ToString("D"));

        string? traceParent =
            activity?.Id
            ?? context.TraceParent;

        string? traceState =
            activity?.TraceStateString
            ?? context.TraceState;

        if (!string.IsNullOrWhiteSpace(traceParent))
        {
            properties.Headers[RabbitMqHeaders.TraceParent] =
                Encoding.UTF8.GetBytes(traceParent);
        }

        if (!string.IsNullOrWhiteSpace(traceState))
        {
            properties.Headers[RabbitMqHeaders.TraceState] =
                Encoding.UTF8.GetBytes(traceState);
        }
    }

    private static string? ReadHeader(
        IReadOnlyDictionary<string, object?>? headers,
        string name)
    {
        if (headers is null
            || !headers.TryGetValue(name, out object? value)
            || value is null)
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory =>
                Encoding.UTF8.GetString(memory.Span),
            string text => text,
            _ => value.ToString()
        };
    }
}
