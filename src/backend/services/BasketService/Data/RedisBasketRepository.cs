using System.Text.Json;
using BasketService.Domain;
using BasketService.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace BasketService.Data;

public sealed class RedisBasketRepository(
    IDistributedCache distributedCache,
    IOptions<BasketOptions> basketOptions) : IBasketRepository
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly BasketOptions _basketOptions = basketOptions.Value;

    public async Task<ShoppingBasket?> GetAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        string key = BasketKeyFactory.Create(customerId);

        string? serializedBasket = await distributedCache.GetStringAsync(
            key,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(serializedBasket))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ShoppingBasket>(
            serializedBasket,
            SerializerOptions);
    }

    public async Task SetAsync(
        ShoppingBasket basket,
        CancellationToken cancellationToken)
    {
        string key = BasketKeyFactory.Create(basket.CustomerId);

        string serializedBasket = JsonSerializer.Serialize(
            basket,
            SerializerOptions);

        DistributedCacheEntryOptions cacheOptions = new()
        {
            AbsoluteExpirationRelativeToNow =
                TimeSpan.FromMinutes(_basketOptions.ExpirationMinutes)
        };

        await distributedCache.SetStringAsync(
            key,
            serializedBasket,
            cacheOptions,
            cancellationToken);
    }

    public Task DeleteAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        string key = BasketKeyFactory.Create(customerId);

        return distributedCache.RemoveAsync(key, cancellationToken);
    }
}
