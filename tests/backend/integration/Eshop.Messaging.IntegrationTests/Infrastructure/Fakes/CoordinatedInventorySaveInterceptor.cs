using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Eshop.Messaging.IntegrationTests.Infrastructure.Fakes;

public sealed class CoordinatedInventorySaveInterceptor(
    int requiredParticipants)
    : SaveChangesInterceptor
{
    private readonly TaskCompletionSource _release =
        new(
            TaskCreationOptions
                .RunContinuationsAsynchronously);

    private int _firstWaveArrivals;

    private int _saveAttemptCount;

    public int FirstWaveArrivals =>
        Volatile.Read(ref _firstWaveArrivals);

    public int SaveAttemptCount =>
        Volatile.Read(ref _saveAttemptCount);

    public void Release()
    {
        _release.TrySetResult();
    }

    public override async ValueTask<
        InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
    {
        int saveAttempt =
            Interlocked.Increment(
                ref _saveAttemptCount);

        if (saveAttempt > requiredParticipants)
        {
            return result;
        }

        int arrivals =
            Interlocked.Increment(
                ref _firstWaveArrivals);

        if (arrivals == requiredParticipants)
        {
            Release();
        }

        await _release.Task.WaitAsync(
            TimeSpan.FromSeconds(15),
            cancellationToken);

        return result;
    }
}
