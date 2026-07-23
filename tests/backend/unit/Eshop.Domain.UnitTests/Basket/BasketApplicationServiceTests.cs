using BasketService.Application;
using BasketService.Data;
using BasketService.Domain;
using BasketService.Integration;
using BasketService.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace Eshop.Domain.UnitTests.Basket;

public sealed class BasketApplicationServiceTests
{
    private const string CustomerId = "customer-1";

    [Fact]
    public async Task AddItem_ProductNotFound_ReturnsNotFound()
    {
        InMemoryBasketRepository repository = new();

        StubCatalogClient catalogClient = new()
        {
            Product = null
        };

        BasketApplicationService service =
            CreateService(
                repository,
                catalogClient);

        BasketMutationResult result =
            await service.AddItemAsync(
                CustomerId,
                Guid.NewGuid(),
                quantity: 1,
                CancellationToken.None);

        Assert.Equal(
            BasketMutationStatus.NotFound,
            result.Status);

        Assert.Null(result.Basket);

        Assert.Equal(
            "Product was not found in Catalog Service.",
            result.Error);

        Assert.Equal(0, repository.SetCallCount);
    }

    [Fact]
    public async Task AddItem_InactiveProduct_ReturnsValidationFailure()
    {
        InMemoryBasketRepository repository = new();

        StubCatalogClient catalogClient = new()
        {
            Product = CreateProduct(
                isActive: false)
        };

        BasketApplicationService service =
            CreateService(
                repository,
                catalogClient);

        BasketMutationResult result =
            await service.AddItemAsync(
                CustomerId,
                catalogClient.Product.Id,
                quantity: 1,
                CancellationToken.None);

        Assert.Equal(
            BasketMutationStatus.ValidationFailed,
            result.Status);

        Assert.Equal(
            "Inactive products cannot be added to the basket.",
            result.Error);

        Assert.Equal(0, repository.SetCallCount);
    }

    [Fact]
    public async Task AddItem_ActiveProduct_PersistsUpdatedBasket()
    {
        InMemoryBasketRepository repository = new();

        CatalogProductSnapshot product =
            CreateProduct(isActive: true);

        StubCatalogClient catalogClient = new()
        {
            Product = product
        };

        BasketApplicationService service =
            CreateService(
                repository,
                catalogClient);

        BasketMutationResult result =
            await service.AddItemAsync(
                CustomerId,
                product.Id,
                quantity: 2,
                CancellationToken.None);

        Assert.Equal(
            BasketMutationStatus.Success,
            result.Status);

        Assert.NotNull(result.Basket);
        Assert.Equal(1, repository.SetCallCount);

        ShoppingBasket storedBasket =
            Assert.IsType<ShoppingBasket>(
                repository.Basket);

        Assert.Equal(CustomerId, storedBasket.CustomerId);

        BasketItem item =
            Assert.Single(storedBasket.Items);

        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(product.Name, item.ProductName);
        Assert.Equal(product.PriceAmount, item.UnitPrice);
        Assert.Equal(product.Currency, item.Currency);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public async Task AddItem_InvalidQuantity_DoesNotPersistBasket()
    {
        InMemoryBasketRepository repository = new();

        CatalogProductSnapshot product =
            CreateProduct(isActive: true);

        BasketApplicationService service =
            CreateService(
                repository,
                new StubCatalogClient
                {
                    Product = product
                });

        BasketMutationResult result =
            await service.AddItemAsync(
                CustomerId,
                product.Id,
                quantity: 0,
                CancellationToken.None);

        Assert.Equal(
            BasketMutationStatus.ValidationFailed,
            result.Status);

        Assert.Equal(
            "Quantity must be between 1 and 5.",
            result.Error);

        Assert.Equal(0, repository.SetCallCount);
    }

    [Fact]
    public async Task UpdateQuantity_MissingItem_ReturnsNotFound()
    {
        InMemoryBasketRepository repository = new();

        BasketApplicationService service =
            CreateService(
                repository,
                new StubCatalogClient());

        BasketMutationResult result =
            await service.UpdateQuantityAsync(
                CustomerId,
                Guid.NewGuid(),
                quantity: 2,
                CancellationToken.None);

        Assert.Equal(
            BasketMutationStatus.NotFound,
            result.Status);

        Assert.Equal(
            "Product is not present in the basket.",
            result.Error);

        Assert.Equal(0, repository.SetCallCount);
    }

    [Fact]
    public async Task RemoveItem_LastItem_DeletesBasket()
    {
        Guid productId = Guid.NewGuid();

        InMemoryBasketRepository repository = new()
        {
            Basket = new ShoppingBasket(
                CustomerId,
                [
                    new BasketItem(
                        productId,
                        "Keyboard",
                        UnitPrice: 125m,
                        Currency: "CZK",
                        Quantity: 1)
                ],
                DateTimeOffset.UtcNow)
        };

        BasketApplicationService service =
            CreateService(
                repository,
                new StubCatalogClient());

        bool removed = await service.RemoveItemAsync(
            CustomerId,
            productId,
            CancellationToken.None);

        Assert.True(removed);
        Assert.Equal(1, repository.DeleteCallCount);
        Assert.Null(repository.Basket);
    }

    [Fact]
    public async Task Clear_ExistingBasket_DeletesBasket()
    {
        InMemoryBasketRepository repository = new()
        {
            Basket = ShoppingBasket.Empty(
                CustomerId,
                DateTimeOffset.UtcNow)
        };

        BasketApplicationService service =
            CreateService(
                repository,
                new StubCatalogClient());

        await service.ClearAsync(
            CustomerId,
            CancellationToken.None);

        Assert.Equal(1, repository.DeleteCallCount);
        Assert.Null(repository.Basket);
    }

    private static BasketApplicationService CreateService(
        IBasketRepository repository,
        ICatalogClient catalogClient)
    {
        return new BasketApplicationService(
            repository,
            catalogClient,
            Options.Create(
                new BasketOptions
                {
                    ExpirationMinutes = 1_440,
                    MaxQuantityPerItem = 5
                }));
    }

    private static CatalogProductSnapshot CreateProduct(
        bool isActive)
    {
        return new CatalogProductSnapshot(
            Guid.NewGuid(),
            "Keyboard",
            PriceAmount: 125m,
            Currency: "CZK",
            isActive);
    }

    private sealed class StubCatalogClient
        : ICatalogClient
    {
        public CatalogProductSnapshot? Product
        {
            get;
            init;
        }

        public Task<CatalogProductSnapshot?>
            GetProductAsync(
                Guid productId,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CatalogProductSnapshot? result =
                Product?.Id == productId
                    ? Product
                    : null;

            return Task.FromResult(result);
        }
    }

    private sealed class InMemoryBasketRepository
        : IBasketRepository
    {
        public ShoppingBasket? Basket
        {
            get;
            set;
        }

        public int SetCallCount
        {
            get;
            private set;
        }

        public int DeleteCallCount
        {
            get;
            private set;
        }

        public Task<ShoppingBasket?> GetAsync(
            string customerId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ShoppingBasket? result =
                Basket?.CustomerId == customerId
                    ? Basket
                    : null;

            return Task.FromResult(result);
        }

        public Task SetAsync(
            ShoppingBasket basket,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Basket = basket;
            SetCallCount++;

            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            string customerId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Basket?.CustomerId == customerId)
            {
                Basket = null;
            }

            DeleteCallCount++;

            return Task.CompletedTask;
        }
    }
}
