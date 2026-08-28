namespace Eshop.Operations.Desktop.Api.Catalog;

public sealed record CatalogProductDto(
    Guid Id,
    string Name,
    string Sku,
    string Description,
    string Category,
    decimal PriceAmount,
    string Currency,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
