namespace BasketService.Domain;

public sealed record ShoppingBasket(
    string CustomerId,
    BasketItem[] Items,
    DateTimeOffset UpdatedAtUtc)
{
    public static ShoppingBasket Empty(
        string customerId,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);

        return new ShoppingBasket(
            customerId,
            [],
            createdAtUtc);
    }

    public bool TryAddOrIncrease(
        BasketItem newItem,
        int maxQuantityPerItem,
        DateTimeOffset updatedAtUtc,
        out ShoppingBasket updatedBasket,
        out string? error)
    {
        BasketItem? existingItem = Items.FirstOrDefault(
            item => item.ProductId == newItem.ProductId);

        if (existingItem is null)
        {
            if (newItem.Quantity > maxQuantityPerItem)
            {
                updatedBasket = this;
                error = $"Maximum quantity per product is {maxQuantityPerItem}.";
                return false;
            }

            updatedBasket = this with
            {
                Items = [.. Items, newItem],
                UpdatedAtUtc = updatedAtUtc
            };

            error = null;
            return true;
        }

        int newQuantity = existingItem.Quantity + newItem.Quantity;

        if (newQuantity > maxQuantityPerItem)
        {
            updatedBasket = this;
            error = $"Maximum quantity per product is {maxQuantityPerItem}.";
            return false;
        }

        BasketItem updatedItem = newItem with
        {
            Quantity = newQuantity
        };

        updatedBasket = this with
        {
            Items = Items
                .Select(item => item.ProductId == updatedItem.ProductId
                    ? updatedItem
                    : item)
                .ToArray(),
            UpdatedAtUtc = updatedAtUtc
        };

        error = null;
        return true;
    }

    public bool TryUpdateQuantity(
        Guid productId,
        int quantity,
        int maxQuantityPerItem,
        DateTimeOffset updatedAtUtc,
        out ShoppingBasket updatedBasket,
        out string? error)
    {
        BasketItem? existingItem = Items.FirstOrDefault(
            item => item.ProductId == productId);

        if (existingItem is null)
        {
            updatedBasket = this;
            error = "Product is not present in the basket.";
            return false;
        }

        if (quantity < 1 || quantity > maxQuantityPerItem)
        {
            updatedBasket = this;
            error = $"Quantity must be between 1 and {maxQuantityPerItem}.";
            return false;
        }

        updatedBasket = this with
        {
            Items = Items
                .Select(item => item.ProductId == productId
                    ? item with { Quantity = quantity }
                    : item)
                .ToArray(),
            UpdatedAtUtc = updatedAtUtc
        };

        error = null;
        return true;
    }

    public bool TryRemove(
        Guid productId,
        DateTimeOffset updatedAtUtc,
        out ShoppingBasket updatedBasket)
    {
        bool itemExists = Items.Any(item => item.ProductId == productId);

        if (!itemExists)
        {
            updatedBasket = this;
            return false;
        }

        updatedBasket = this with
        {
            Items = Items
                .Where(item => item.ProductId != productId)
                .ToArray(),
            UpdatedAtUtc = updatedAtUtc
        };

        return true;
    }
}
