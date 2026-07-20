namespace PaymentsService.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string RoutingKey { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public Guid CorrelationId { get; private set; }

    public string? TraceParent { get; private set; }

    public string? TraceState { get; private set; }

    public OutboxMessageStatus Status { get; private set; }

    public int RetryCount { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public DateTimeOffset? ClaimedAtUtc { get; private set; }

    public string? ClaimedBy { get; private set; }

    public static OutboxMessage Create(
        Guid id,
        Guid eventId,
        string eventType,
        string routingKey,
        string payload,
        DateTimeOffset occurredAtUtc,
        Guid correlationId,
        string? traceParent,
        string? traceState)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Outbox message id must not be empty.",
                nameof(id));
        }

        if (eventId == Guid.Empty)
        {
            throw new ArgumentException(
                "Event id must not be empty.",
                nameof(eventId));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Correlation id must not be empty.",
                nameof(correlationId));
        }

        return new OutboxMessage
        {
            Id = id,
            EventId = eventId,
            EventType = Required(eventType, nameof(eventType), 256),
            RoutingKey = Required(routingKey, nameof(routingKey), 256),
            Payload = Required(payload, nameof(payload), 1_000_000),
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = correlationId,
            TraceParent = Optional(traceParent, 512),
            TraceState = Optional(traceState, 512),
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0
        };
    }

    public void Claim(
        string workerId,
        DateTimeOffset claimedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        Status = OutboxMessageStatus.Processing;
        ClaimedBy = workerId;
        ClaimedAtUtc = claimedAtUtc;
        NextAttemptAtUtc = null;
    }

    public void MarkPublished(
        DateTimeOffset publishedAtUtc)
    {
        Status = OutboxMessageStatus.Published;
        PublishedAtUtc = publishedAtUtc;
        LastError = null;
        NextAttemptAtUtc = null;
        ClaimedAtUtc = null;
        ClaimedBy = null;
    }

    public void MarkFailed(
        string error,
        DateTimeOffset failedAtUtc,
        int maximumRetryCount,
        TimeSpan retryDelay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        ArgumentOutOfRangeException.ThrowIfLessThan(maximumRetryCount, 1);

        ArgumentOutOfRangeException.ThrowIfLessThan(retryDelay, TimeSpan.Zero);

        int nextRetryCount =
            checked(RetryCount + 1);

        RetryCount = nextRetryCount;
        LastError = error;
        ClaimedAtUtc = null;
        ClaimedBy = null;

        if (nextRetryCount >= maximumRetryCount)
        {
            Status = OutboxMessageStatus.Dead;
            NextAttemptAtUtc = null;
            return;
        }

        Status = OutboxMessageStatus.Failed;
        NextAttemptAtUtc =
            failedAtUtc + retryDelay;
    }

    private static string Required(
        string value,
        string parameterName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Value must not be empty.",
                parameterName);
        }

        string normalizedValue = value.Trim();

        if (normalizedValue.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Value must not exceed {maximumLength} characters.",
                parameterName);
        }

        return normalizedValue;
    }

    private static string? Optional(
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalizedValue = value.Trim();

        return normalizedValue.Length <= maximumLength
            ? normalizedValue
            : normalizedValue[..maximumLength];
    }
}
