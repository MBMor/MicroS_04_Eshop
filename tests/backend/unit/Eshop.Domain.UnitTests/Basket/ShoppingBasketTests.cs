using BasketService.Domain;
using Xunit;

namespace Eshop.Domain.UnitTests.Basket;

public sealed class ShoppingBasketTests
{
    private const int MaxQuantityPerItem = 5;

    private static readonly DateTimeOffset CreatedAtUtc =
        new(
            year: 2026,
            month: 7,
            day: 23,
            hour: 8,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero);

    [Fact]
    public void Empty_ValidCustomer_CreatesEmptyBasket()
    {
        ShoppingBasket basket = ShoppingBasket.Empty(
            "customer-1",
            CreatedAtUtc);

        Assert.Equal("customer-1", basket.CustomerId);
        Assert.Empty(basket.Items);
        Assert.Equal(CreatedAtUtc, basket.UpdatedAtUtc);
    }

    [Fact]
    public void Empty_BlankCustomer_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => ShoppingBasket.Empty(
                "   ",
                CreatedAtUtc));
    }

    [Fact]
    public void TryAddOrIncrease_NewItem_AddsItem()
    {
        ShoppingBasket basket = CreateEmptyBasket();

        BasketItem item = CreateItem(
            quantity: 2);

        DateTimeOffset updatedAtUtc =
            CreatedAtUtc.AddMinutes(1);

        bool succeeded = basket.TryAddOrIncrease(
            item,
            MaxQuantityPerItem,
            updatedAtUtc,
            out ShoppingBasket updatedBasket,
            out string? error);

        Assert.True(succeeded);
        Assert.Null(error);
        Assert.Single(updatedBasket.Items);
        Assert.Equal(item, updatedBasket.Items[0]);
        Assert.Equal(updatedAtUtc, updatedBasket.UpdatedAtUtc);
        Assert.Empty(basket.Items);
    }

    [Fact]
    public void TryAddOrIncrease_ExistingItem_IncreasesQuantity()
    {
        Guid productId = Guid.NewGuid();

        ShoppingBasket basket = new(
            "customer-1",
            [
                CreateItem(
                    productId,
                    quantity: 2)
            ],
            CreatedAtUtc);

        BasketItem additionalItem = CreateItem(
            productId,
            quantity: 3);

        bool succeeded = basket.TryAddOrIncrease(
            additionalItem,
            MaxQuantityPerItem,
            CreatedAtUtc.AddMinutes(1),
            out ShoppingBasket updatedBasket,
            out string? error);

        Assert.True(succeeded);
        Assert.Null(error);

        BasketItem updatedItem =
            Assert.Single(updatedBasket.Items);

        Assert.Equal(5, updatedItem.Quantity);

        BasketItem originalItem =
            Assert.Single(basket.Items);

        Assert.Equal(2, originalItem.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryAddOrIncrease_NonPositiveQuantity_FailsWithoutMutation(
        int quantity)
    {
        ShoppingBasket basket = CreateEmptyBasket();

        bool succeeded = basket.TryAddOrIncrease(
            CreateItem(quantity: quantity),
            MaxQuantityPerItem,
            CreatedAtUtc.AddMinutes(1),
            out ShoppingBasket updatedBasket,
            out string? error);

        Assert.False(succeeded);
        Assert.Same(basket, updatedBasket);

        Assert.Equal(
            "Quantity must be between 1 and 5.",
            error);

        Assert.Empty(basket.Items);
    }

    [Fact]
    public void TryAddOrIncrease_QuantityAboveMaximum_Fails()
    {
        ShoppingBasket basket = CreateEmptyBasket();

        bool succeeded = basket.TryAddOrIncrease(
            CreateItem(quantity: 6),
            MaxQuantityPerItem,
            CreatedAtUtc.AddMinutes(1),
            out ShoppingBasket updatedBasket,
            out string? error);

        Assert.False(succeeded);
        Assert.Same(basket, updatedBasket);

        Assert.Equal(
            "Maximum quantity per product is 5.",
            error);
    }

    [Fact]
    public void TryUpdateQuantity_ExistingItem_UpdatesQuantity()
    {
        Guid productId = Guid.NewGuid();

        ShoppingBasket basket = CreateBasketWithItem(
            productId,
            quantity: 2);

        bool succeeded = basket.TryUpdateQuantity(
            productId,
            quantity: 4,
            MaxQuantityPerItem,
            CreatedAtUtc.AddMinutes(1),
            out ShoppingBasket updatedBasket,
            out string? error);

        Assert.True(succeeded);
        Assert.Null(error);

        BasketItem item = Assert.Single(
            updatedBasket.Items);

        Assert.Equal(4, item.Quantity);
    }

    [Fact]
    public void TryUpdateQuantity_MissingItem_Fails()
    {
        ShoppingBasket basket = CreateEmptyBasket();

        bool succeeded = basket.TryUpdateQuantity(
            Guid.NewGuid(),
            quantity: 2,
            MaxQuantityPerItem,
            CreatedAtUtc.AddMinutes(1),
            out ShoppingBasket updatedBasket,
            out string? error);

        Assert.False(succeeded);
        Assert.Same(basket, updatedBasket);

        Assert.Equal(
            "Product is not present in the basket.",
            error);
    }

    [Fact]
    public void TryUpdateQuantity_InvalidQuantity_FailsWithoutMutation()
    {
        Guid productId = Guid.NewGuid();

        ShoppingBasket basket = CreateBasketWithItem(
            productId,
            quantity: 2);

        bool succeeded = basket.TryUpdateQuantity(
            productId,
            quantity: 0,
            MaxQuantityPerItem,
            CreatedAtUtc.AddMinutes(1),
            out ShoppingBasket updatedBasket,
            out string? error);

        Assert.False(succeeded);
        Assert.Same(basket, updatedBasket);

        Assert.Equal(
            "Quantity must be between 1 and 5.",
            error);

        Assert.Equal(
            2,
            Assert.Single(basket.Items).Quantity);
    }

    [Fact]
    public void TryRemove_ExistingItem_RemovesItem()
    {
        Guid productId = Guid.NewGuid();

        ShoppingBasket basket = CreateBasketWithItem(
            productId,
            quantity: 2);

        bool succeeded = basket.TryRemove(
            productId,
            CreatedAtUtc.AddMinutes(1),
            out ShoppingBasket updatedBasket);

        Assert.True(succeeded);
        Assert.Empty(updatedBasket.Items);
        Assert.Single(basket.Items);
    }

    [Fact]
    public void TryRemove_MissingItem_FailsWithoutMutation()
    {
        ShoppingBasket basket = CreateEmptyBasket();

        bool succeeded = basket.TryRemove(
            Guid.NewGuid(),
            CreatedAtUtc.AddMinutes(1),
            out ShoppingBasket updatedBasket);

        Assert.False(succeeded);
        Assert.Same(basket, updatedBasket);
    }

    [Fact]
    public void BasketItem_LineTotal_MultipliesPriceAndQuantity()
    {
        BasketItem item = new(
            Guid.NewGuid(),
            "Keyboard",
            UnitPrice: 125m,
            Currency: "CZK",
            Quantity: 3);

        Assert.Equal(375m, item.LineTotal);
    }

    private static ShoppingBasket CreateEmptyBasket()
    {
        return ShoppingBasket.Empty(
            "customer-1",
            CreatedAtUtc);
    }

    private static ShoppingBasket CreateBasketWithItem(
        Guid productId,
        int quantity)
    {
        return new ShoppingBasket(
            "customer-1",
            [
                CreateItem(
                    productId,
                    quantity)
            ],
            CreatedAtUtc);
    }

    private static BasketItem CreateItem(
        int quantity)
    {
        return CreateItem(
            Guid.NewGuid(),
            quantity);
    }

    private static BasketItem CreateItem(
        Guid productId,
        int quantity)
    {
        return new BasketItem(
            productId,
            "Keyboard",
            UnitPrice: 125m,
            Currency: "CZK",
            quantity);
    }
}
