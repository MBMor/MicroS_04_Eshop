namespace Messaging.Shared.Outbox;

public static class OutboxRetryPolicy
{
    private const int MaximumExponent = 30;

    public static TimeSpan CalculateDelay(
        OutboxProcessingOptions options,
        int currentRetryCount)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(
            currentRetryCount);

        int exponent =
            Math.Min(
                currentRetryCount,
                MaximumExponent);

        double multiplier =
            Math.Pow(2, exponent);

        double calculatedTicks =
            options.InitialRetryDelay.Ticks
            * multiplier;

        double boundedTicks =
            Math.Min(
                calculatedTicks,
                options.MaximumRetryDelay.Ticks);

        return TimeSpan.FromTicks(
            checked((long)boundedTicks));
    }
}
