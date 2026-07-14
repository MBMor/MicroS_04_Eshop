using CatalogService.Domain;

namespace CatalogService.Contracts;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Sku,
    string Description,
    string Category,
    decimal PriceAmount,
    string Currency,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc)
{
    public static ProductResponse FromProduct(Product product)
    {
        return new ProductResponse(
            product.Id,
            product.Name,
            product.Sku,
            product.Description,
            product.Category,
            product.PriceAmount,
            product.Currency,
            product.IsActive,
            product.CreatedAtUtc,
            product.UpdatedAtUtc);
    }
}
