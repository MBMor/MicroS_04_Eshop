using System.Diagnostics;
using Messaging.Shared.Contracts;
using Messaging.Shared.RabbitMq;

namespace Messaging.Shared.Telemetry;

public static class MessagingActivity
{
    public static Activity? StartPublish(
        string exchange,
        string routingKey,
        string eventType,
        Guid eventId,
        MessagePublishContext context)
    {
        ActivityContext parentContext = ParseParentContext(context);

        Activity? activity =
            parentContext != default
                ? MessagingTelemetry.ActivitySource.StartActivity(
                    "rabbitmq.publish",
                    ActivityKind.Producer,
                    parentContext)
                : MessagingTelemetry.ActivitySource.StartActivity(
                    "rabbitmq.publish",
                    ActivityKind.Producer);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(
            "messaging.system",
            "rabbitmq");

        activity.SetTag(
            "messaging.destination.name",
            exchange);

        activity.SetTag(
            "messaging.rabbitmq.routing_key",
            routingKey);

        activity.SetTag(
            "messaging.operation.type",
            "publish");

        activity.SetTag(
            "messaging.message.id",
            eventId.ToString("D"));

        activity.SetTag(
            "messaging.message.type",
            eventType);

        activity.SetTag(
            "eshop.correlation_id",
            context.CorrelationId.ToString("D"));

        return activity;
    }

    public static Activity? StartConsume(
        string queueName,
        string routingKey,
        string eventType,
        Guid eventId,
        Guid correlationId,
        IReadOnlyDictionary<string, object?>? headers)
    {
        ActivityContext parentContext =
            RabbitMqTraceContext.ExtractParentContext(headers);

        Activity? activity =
            parentContext != default
                ? MessagingTelemetry.ActivitySource.StartActivity(
                    "rabbitmq.consume",
                    ActivityKind.Consumer,
                    parentContext)
                : MessagingTelemetry.ActivitySource.StartActivity(
                    "rabbitmq.consume",
                    ActivityKind.Consumer);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(
            "messaging.system",
            "rabbitmq");

        activity.SetTag(
            "messaging.destination.name",
            queueName);

        activity.SetTag(
            "messaging.rabbitmq.routing_key",
            routingKey);

        activity.SetTag(
            "messaging.operation.type",
            "process");

        activity.SetTag(
            "messaging.message.id",
            eventId.ToString("D"));

        activity.SetTag(
            "messaging.message.type",
            eventType);

        activity.SetTag(
            "eshop.correlation_id",
            correlationId.ToString("D"));

        return activity;
    }

    public static void RecordFailure(
        Activity? activity,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        activity?.SetStatus(
            ActivityStatusCode.Error,
            exception.Message);

        activity?.AddException(exception);
    }

    private static ActivityContext ParseParentContext(
        MessagePublishContext context)
    {
        if (string.IsNullOrWhiteSpace(context.TraceParent))
        {
            return default;
        }

        return ActivityContext.TryParse(
            context.TraceParent,
            context.TraceState,
            out ActivityContext parentContext)
                ? parentContext
                : default;
    }
}
