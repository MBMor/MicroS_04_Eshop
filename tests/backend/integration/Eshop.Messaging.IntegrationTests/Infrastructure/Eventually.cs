using System.Diagnostics;
using Xunit.Sdk;

namespace Eshop.Messaging.IntegrationTests.Infrastructure;

public static class Eventually
{
    private static readonly TimeSpan DefaultTimeout =
        TimeSpan.FromSeconds(15);

    private static readonly TimeSpan DefaultPollInterval =
        TimeSpan.FromMilliseconds(100);

    public static async Task UntilAsync(
        Func<CancellationToken, Task<bool>> condition,
        string description,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        TimeSpan effectiveTimeout =
            timeout ?? DefaultTimeout;

        TimeSpan effectivePollInterval =
            pollInterval ?? DefaultPollInterval;

        ValidateDurations(
            effectiveTimeout,
            effectivePollInterval);

        long startedAt =
            Stopwatch.GetTimestamp();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await condition(cancellationToken))
            {
                return;
            }

            TimeSpan elapsed =
                Stopwatch.GetElapsedTime(startedAt);

            if (elapsed >= effectiveTimeout)
            {
                throw new TimeoutException(
                    $"Condition was not satisfied within " +
                    $"{effectiveTimeout}: {description}.");
            }

            await DelayAsync(
                effectiveTimeout - elapsed,
                effectivePollInterval,
                cancellationToken);
        }
    }

    public static async Task SucceedsAsync(
        Func<CancellationToken, Task> assertion,
        string description,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        TimeSpan effectiveTimeout =
            timeout ?? DefaultTimeout;

        TimeSpan effectivePollInterval =
            pollInterval ?? DefaultPollInterval;

        ValidateDurations(
            effectiveTimeout,
            effectivePollInterval);

        long startedAt =
            Stopwatch.GetTimestamp();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await assertion(cancellationToken);
                return;
            }
            catch (XunitException exception)
            {
                TimeSpan elapsed =
                    Stopwatch.GetElapsedTime(startedAt);

                if (elapsed >= effectiveTimeout)
                {
                    throw new TimeoutException(
                        $"Assertion did not succeed within " +
                        $"{effectiveTimeout}: {description}.",
                        exception);
                }

                await DelayAsync(
                    effectiveTimeout - elapsed,
                    effectivePollInterval,
                    cancellationToken);
            }
        }
    }

    private static async Task DelayAsync(
        TimeSpan remaining,
        TimeSpan pollInterval,
        CancellationToken cancellationToken)
    {
        TimeSpan delay =
            remaining < pollInterval
                ? remaining
                : pollInterval;

        await Task.Delay(
            delay,
            cancellationToken);
    }

    private static void ValidateDurations(
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);

        if (pollInterval > timeout)
        {
            throw new ArgumentException(
                "Polling interval must not exceed timeout.",
                nameof(pollInterval));
        }
    }
}
