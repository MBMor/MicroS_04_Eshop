namespace Messaging.Shared.Outbox;

public sealed class OutboxProcessingOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; set; } = 20;

    public TimeSpan PollingInterval { get; set; } =
        TimeSpan.FromSeconds(2);

    public TimeSpan ClaimTimeout { get; set; } =
        TimeSpan.FromMinutes(2);

    public TimeSpan PublishedRetention { get; set; } =
        TimeSpan.FromDays(7);

    public TimeSpan CleanupInterval { get; set; } =
        TimeSpan.FromHours(6);

    public int CleanupBatchSize { get; set; } = 500;
}
