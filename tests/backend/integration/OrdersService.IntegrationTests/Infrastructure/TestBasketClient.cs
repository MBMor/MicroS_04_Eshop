using System.Collections.Concurrent;
using OrdersService.Integration;

namespace OrdersService.IntegrationTests.Infrastructure;

internal sealed class TestBasketClient
    : IBasketClient
{
    private readonly ConcurrentDictionary<
        string,
        BasketSnapshot> _baskets =
            new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<
        string,
        int> _clearCallCounts =
            new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<
        string,
        int> _getCallCounts =
            new(StringComparer.Ordinal);

    private TaskCompletionSource?
        _basketReadGate;

    private string? _gatedCustomerId;

    private int _remainingGatedReads;

    private HttpRequestException? _clearFailure;

    public void SetBasket(
        string customerId,
        params BasketItemSnapshot[] items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            customerId);

        ArgumentNullException.ThrowIfNull(items);

        _baskets[customerId] =
            new BasketSnapshot(items);
    }

    public int GetClearCallCount(
        string customerId)
    {
        return _clearCallCounts.TryGetValue(
            customerId,
            out int count)
                ? count
                : 0;
    }

    public int GetBasketCallCount(
        string customerId)
    {
        return _getCallCounts.TryGetValue(
            customerId,
            out int count)
                ? count
                : 0;
    }

    public void FailBasketClear()
    {
        _clearFailure = new HttpRequestException(
            "Injected basket clear failure.");
    }

    public void SynchronizeNextBasketReads(
        string customerId,
        int participantCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            customerId);

        ArgumentOutOfRangeException.ThrowIfLessThan(
            participantCount,
            2);

        _gatedCustomerId = customerId;
        _remainingGatedReads = participantCount;
        _basketReadGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Reset()
    {
        _baskets.Clear();
        _clearCallCounts.Clear();
        _getCallCounts.Clear();
        _basketReadGate?.TrySetResult();
        _basketReadGate = null;
        _gatedCustomerId = null;
        _remainingGatedReads = 0;
        _clearFailure = null;
    }

    public async Task<BasketSnapshot> GetBasketAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _getCallCounts.AddOrUpdate(
            customerId,
            addValue: 1,
            static (_, current) => current + 1);

        TaskCompletionSource? readGate =
            _basketReadGate;

        if (readGate is not null
            && string.Equals(
                customerId,
                _gatedCustomerId,
                StringComparison.Ordinal))
        {
            int remainingReads =
                Interlocked.Decrement(
                    ref _remainingGatedReads);

            if (remainingReads == 0)
            {
                readGate.TrySetResult();
            }

            if (remainingReads >= 0)
            {
                await readGate.Task.WaitAsync(
                    cancellationToken);
            }
        }

        BasketSnapshot basket =
            _baskets.TryGetValue(
                customerId,
                out BasketSnapshot? existingBasket)
                    ? existingBasket
                    : new BasketSnapshot([]);

        return basket;
    }

    public Task ClearBasketAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _clearCallCounts.AddOrUpdate(
            customerId,
            addValue: 1,
            static (_, current) => current + 1);

        if (_clearFailure is not null)
        {
            throw _clearFailure;
        }

        _baskets.TryRemove(
            customerId,
            out _);

        return Task.CompletedTask;
    }
}
