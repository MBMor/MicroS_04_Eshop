namespace Eshop.Messaging.IntegrationTests.Infrastructure.Fakes;

public sealed class TransientConsumerFailureState
{
    private readonly object _syncRoot =
        new();

    private readonly Dictionary<Guid, int> _remainingFailures =
        new();

    private readonly Dictionary<Guid, int> _attemptCounts =
        new();

    public void FailNext(
        Guid eventId,
        int failureCount = 1)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException(
                "Event ID must not be empty.",
                nameof(eventId));
        }

        if (failureCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureCount),
                failureCount,
                "Failure count must be greater than zero.");
        }

        lock (_syncRoot)
        {
            _remainingFailures[eventId] =
                failureCount;

            _attemptCounts.Remove(
                eventId);
        }
    }

    public bool RecordAttemptAndShouldFail(
        Guid eventId)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException(
                "Event ID must not be empty.",
                nameof(eventId));
        }

        lock (_syncRoot)
        {
            _attemptCounts.TryGetValue(
                eventId,
                out int currentAttemptCount);

            _attemptCounts[eventId] =
                checked(currentAttemptCount + 1);

            if (!_remainingFailures.TryGetValue(
                    eventId,
                    out int remainingFailures))
            {
                return false;
            }

            if (remainingFailures == 1)
            {
                _remainingFailures.Remove(
                    eventId);
            }
            else
            {
                _remainingFailures[eventId] =
                    remainingFailures - 1;
            }

            return true;
        }
    }

    public int GetAttemptCount(
        Guid eventId)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException(
                "Event ID must not be empty.",
                nameof(eventId));
        }

        lock (_syncRoot)
        {
            return _attemptCounts.TryGetValue(
                eventId,
                out int attemptCount)
                    ? attemptCount
                    : 0;
        }
    }

    public void Reset()
    {
        lock (_syncRoot)
        {
            _remainingFailures.Clear();
            _attemptCounts.Clear();
        }
    }
}
