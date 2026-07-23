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

    public void Reset()
    {
        _baskets.Clear();
        _clearCallCounts.Clear();
    }

    public Task<BasketSnapshot> GetBasketAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        BasketSnapshot basket =
            _baskets.TryGetValue(
                customerId,
                out BasketSnapshot? existingBasket)
                    ? existingBasket
                    : new BasketSnapshot([]);

        return Task.FromResult(basket);
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

        _baskets.TryRemove(
            customerId,
            out _);

        return Task.CompletedTask;
    }
}
