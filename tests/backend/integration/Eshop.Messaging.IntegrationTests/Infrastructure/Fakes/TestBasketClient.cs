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

    public void Reset()
    {
        _baskets.Clear();
        _clearedCustomers.Clear();
    }

    public Task<BasketSnapshot> GetBasketAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            customerId);

        cancellationToken.ThrowIfCancellationRequested();

        if (!_baskets.TryGetValue(
                customerId,
                out BasketSnapshot? basket))
        {
            throw new InvalidOperationException(
                $"No test basket is configured for customer " +
                $"'{customerId}'.");
        }

        return Task.FromResult(basket);
    }

    public Task ClearBasketAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            customerId);

        cancellationToken.ThrowIfCancellationRequested();

        _baskets.TryRemove(
            customerId,
            out _);

        _clearedCustomers[customerId] = 0;

        return Task.CompletedTask;
    }
}
