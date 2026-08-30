using InventoryService.Data;
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
    DateTimeOffset? UpdatedAtUtc,
    uint Version)
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
            item.UpdatedAtUtc,
            item.Version);
    }

    public static InventoryItemResponse FromStockAdjustmentOperation(
        InventoryStockAdjustmentOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.Outcome
            != InventoryStockAdjustmentOutcome.Success)
        {
            throw new InvalidOperationException(
                "Only a successful stock adjustment can be mapped to an inventory response.");
        }

        if (operation.ProductId is null
            || string.IsNullOrWhiteSpace(operation.Sku)
            || operation.OnHandAfter is null
            || operation.ReservedAfter is null
            || operation.AvailableAfter is null
            || operation.IsActive is null
            || operation.ItemCreatedAtUtc is null
            || operation.ResultVersion is null)
        {
            throw new InvalidOperationException(
                "The successful stock adjustment audit snapshot is incomplete.");
        }

        return new InventoryItemResponse(
            operation.InventoryItemId,
            operation.ProductId.Value,
            operation.Sku,
            operation.OnHandAfter.Value,
            operation.ReservedAfter.Value,
            operation.AvailableAfter.Value,
            operation.IsActive.Value,
            operation.ItemCreatedAtUtc.Value,
            operation.ItemUpdatedAtUtc,
            checked((uint)operation.ResultVersion.Value));
    }
}
