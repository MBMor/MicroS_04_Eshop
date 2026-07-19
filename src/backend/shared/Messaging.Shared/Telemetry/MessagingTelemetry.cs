using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Messaging.Shared.Telemetry;

public static class MessagingTelemetry
{
    public const string ActivitySourceName =
        "Eshop.Messaging";

    public const string MeterName =
        "Eshop.Messaging";

    public static readonly ActivitySource ActivitySource =
        new(ActivitySourceName);

    public static readonly Meter Meter =
        new(MeterName);

    public static readonly Counter<long> PublishedMessages =
        Meter.CreateCounter<long>(
            "messaging.publish.count",
            description: "Number of published messages.");

    public static readonly Counter<long> ConsumedMessages =
        Meter.CreateCounter<long>(
            "messaging.consume.count",
            description: "Number of consumed messages.");

    public static readonly Counter<long> RetriedMessages =
        Meter.CreateCounter<long>(
            "messaging.retry.count",
            description: "Number of messages returned for retry.");

    public static readonly Counter<long> DeadLetteredMessages =
        Meter.CreateCounter<long>(
            "messaging.dead_letter.count",
            description: "Number of messages rejected to a DLQ.");

    public static readonly Counter<long> DuplicateMessages =
        Meter.CreateCounter<long>(
            "messaging.duplicate.count",
            description: "Number of duplicate messages acknowledged.");

    public static readonly Counter<long> FailedMessages =
        Meter.CreateCounter<long>(
            "messaging.failure.count",
            description: "Number of failed messaging operations.");

    public static readonly Histogram<double> PublishDuration =
        Meter.CreateHistogram<double>(
            "messaging.publish.duration",
            unit: "ms",
            description: "RabbitMQ publish duration.");

    public static readonly Histogram<double> ConsumeDuration =
        Meter.CreateHistogram<double>(
            "messaging.consume.duration",
            unit: "ms",
            description: "RabbitMQ message processing duration.");
}
