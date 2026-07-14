using BasketService.Data;
using BasketService.Domain;
using BasketService.Integration;
using BasketService.Options;
using Microsoft.Extensions.Options;

namespace BasketService.Application;

public sealed class BasketApplicationService(
    IBasketRepository basketRepository,
    ICatalogClient catalogClient,
    IOptions<BasketOptions> basketOptions)
{
    private readonly BasketOptions _basketOptions = basketOptions.Value;

    public async Task<ShoppingBasket> GetAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        ShoppingBasket? basket = await basketRepository.GetAsync(
            customerId,
            cancellationToken);

        return basket ?? ShoppingBasket.Empty(
            customerId,
            DateTimeOffset.UtcNow);
    }

    public async Task<BasketMutationResult> AddItemAsync(
        string customerId,
        Guid productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        CatalogProductSnapshot? product = await catalogClient.GetProductAsync(
            productId,
            cancellationToken);

        if (product is null)
        {
            return BasketMutationResult.NotFound(
                "Product was not found in Catalog Service.");
        }

        if (!product.IsActive)
        {
            return BasketMutationResult.ValidationFailed(
                "Inactive products cannot be added to the basket.");
        }

        ShoppingBasket currentBasket = await GetAsync(
            customerId,
            cancellationToken);

        BasketItem newItem = new(
            product.Id,
            product.Name,
            product.PriceAmount,
            product.Currency,
            quantity);

        bool succeeded = currentBasket.TryAddOrIncrease(
            newItem,
            _basketOptions.MaxQuantityPerItem,
            DateTimeOffset.UtcNow,
            out ShoppingBasket updatedBasket,
            out string? error);

        if (!succeeded)
        {
            return BasketMutationResult.ValidationFailed(
                error ?? "The basket could not be updated.");
        }

        await basketRepository.SetAsync(
            updatedBasket,
            cancellationToken);

        return BasketMutationResult.Succeeded(updatedBasket);
    }

    public async Task<BasketMutationResult> UpdateQuantityAsync(
        string customerId,
        Guid productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        ShoppingBasket currentBasket = await GetAsync(
            customerId,
            cancellationToken);

        bool succeeded = currentBasket.TryUpdateQuantity(
            productId,
            quantity,
            _basketOptions.MaxQuantityPerItem,
            DateTimeOffset.UtcNow,
            out ShoppingBasket updatedBasket,
            out string? error);

        if (!succeeded)
        {
            return BasketMutationResult.NotFound(
                error ?? "Basket item was not found.");
        }

        await basketRepository.SetAsync(
            updatedBasket,
            cancellationToken);

        return BasketMutationResult.Succeeded(updatedBasket);
    }

    public async Task<bool> RemoveItemAsync(
        string customerId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        ShoppingBasket currentBasket = await GetAsync(
            customerId,
            cancellationToken);

        bool succeeded = currentBasket.TryRemove(
            productId,
            DateTimeOffset.UtcNow,
            out ShoppingBasket updatedBasket);

        if (!succeeded)
        {
            return false;
        }

        if (updatedBasket.Items.Length == 0)
        {
            await basketRepository.DeleteAsync(
                customerId,
                cancellationToken);
        }
        else
        {
            await basketRepository.SetAsync(
                updatedBasket,
                cancellationToken);
        }

        return true;
    }

    public Task ClearAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        return basketRepository.DeleteAsync(
            customerId,
            cancellationToken);
    }
}
