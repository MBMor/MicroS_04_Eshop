using InventoryService.Domain;

namespace InventoryService.Contracts;

public sealed record InventoryItemResponse(
    Guid Id,
    Guid ProductId,
    string Sku,
    int OnHandQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc)
{
    public static InventoryItemResponse FromInventoryItem(
        InventoryItem item)
    {
        return new InventoryItemResponse(
            item.Id,
            item.ProductId,
            item.Sku,
            item.OnHandQuantity,
            item.ReservedQuantity,
            item.AvailableQuantity,
            item.IsActive,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }
}
