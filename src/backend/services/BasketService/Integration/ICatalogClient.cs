namespace BasketService.Integration;

public interface ICatalogClient
{
    Task<CatalogProductSnapshot?> GetProductAsync(
        Guid productId,
        CancellationToken cancellationToken);
}
