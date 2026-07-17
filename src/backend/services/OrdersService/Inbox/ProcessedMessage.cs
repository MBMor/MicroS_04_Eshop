namespace OrdersService.Inbox;

public sealed class ProcessedMessage
{
    private ProcessedMessage()
    {
    }

    public Guid EventId { get; private set; }

    public string ConsumerName { get; private set; } = string.Empty;

    public DateTimeOffset ProcessedAtUtc { get; private set; }

    public static ProcessedMessage Create(
        Guid eventId,
        string consumerName,
        DateTimeOffset processedAtUtc)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException(
                "Event id must not be empty.",
                nameof(eventId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(
            consumerName);

        return new ProcessedMessage
        {
            EventId = eventId,
            ConsumerName = consumerName.Trim(),
            ProcessedAtUtc = processedAtUtc
        };
    }
}
