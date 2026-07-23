using System.Collections.Concurrent;
using BasketService.Integration;

namespace BasketService.IntegrationTests.Infrastructure;

internal sealed class TestCatalogClient
    : ICatalogClient
{
    private readonly ConcurrentDictionary<
        Guid,
        CatalogProductSnapshot> _products = new();

    public CatalogProductSnapshot RegisterProduct(
        bool isActive = true,
        string name = "Mechanical Keyboard",
        decimal priceAmount = 2_500m,
        string currency = "CZK")
    {
        CatalogProductSnapshot product = new(
            Guid.NewGuid(),
            name,
            priceAmount,
            currency,
            isActive);

        _products[product.Id] = product;

        return product;
    }

    public Task<CatalogProductSnapshot?>
        GetProductAsync(
            Guid productId,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _products.TryGetValue(
            productId,
            out CatalogProductSnapshot? product);

        return Task.FromResult(product);
    }
}
