using System.Collections.Concurrent;
using OrdersService.Integration;

namespace Eshop.Messaging.IntegrationTests.Infrastructure.Fakes;

public sealed class TestBasketClient : IBasketClient
{
    private readonly ConcurrentDictionary<string, BasketSnapshot>
        _baskets =
            new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte>
        _clearedCustomers =
            new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, int>
        _getCallCounts =
            new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, int>
        _clearCallCounts =
            new(StringComparer.Ordinal);

    private TaskCompletionSource? _basketReadGate;

    private string? _gatedCustomerId;

    private int _remainingGatedReads;

    public void SetBasket(
        string customerId,
        BasketSnapshot basket)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            customerId);

        ArgumentNullException.ThrowIfNull(
            basket);

        _baskets[customerId] = basket;

        _clearedCustomers.TryRemove(
            customerId,
            out _);
    }

    public bool WasCleared(
        string customerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            customerId);

        return _clearedCustomers.ContainsKey(
            customerId);
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

    public int GetClearCallCount(
        string customerId)
    {
        return _clearCallCounts.TryGetValue(
            customerId,
            out int count)
                ? count
                : 0;
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
        _clearedCustomers.Clear();
        _getCallCounts.Clear();
        _clearCallCounts.Clear();
        _basketReadGate?.TrySetResult();
        _basketReadGate = null;
        _gatedCustomerId = null;
        _remainingGatedReads = 0;
    }

    public async Task<BasketSnapshot> GetBasketAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            customerId);

        cancellationToken.ThrowIfCancellationRequested();

        _getCallCounts.AddOrUpdate(
            customerId,
            addValue: 1,
            static (_, current) => current + 1);

        if (!_baskets.TryGetValue(
                customerId,
                out BasketSnapshot? basket))
        {
            throw new InvalidOperationException(
                $"No test basket is configured for customer " +
                $"'{customerId}'.");
        }

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

        return basket;
    }

    public Task ClearBasketAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            customerId);

        cancellationToken.ThrowIfCancellationRequested();

        _clearCallCounts.AddOrUpdate(
            customerId,
            addValue: 1,
            static (_, current) => current + 1);

        _baskets.TryRemove(
            customerId,
            out _);

        _clearedCustomers[customerId] = 0;

        return Task.CompletedTask;
    }
}
