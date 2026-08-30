namespace Eshop.Operations.Desktop.Api.Inventory;

public sealed record InventoryItemDto(
    Guid Id,
    Guid ProductId,
    string Sku,
    int OnHandQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    uint Version);
