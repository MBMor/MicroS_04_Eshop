namespace Eshop.Operations.Desktop.Api.Catalog;

public interface ICatalogApiClient
{
    Task<IReadOnlyList<CatalogProductDto>> GetProductsAsync(
        bool includeInactive,
        CancellationToken cancellationToken);
}
