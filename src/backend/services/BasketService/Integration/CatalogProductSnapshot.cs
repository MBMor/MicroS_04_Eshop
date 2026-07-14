namespace BasketService.Integration;

public sealed record CatalogProductSnapshot(
    Guid Id,
    string Name,
    decimal PriceAmount,
    string Currency,
    bool IsActive);
