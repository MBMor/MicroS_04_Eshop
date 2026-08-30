namespace Eshop.Operations.Desktop.Api.Inventory;

public sealed record InventoryStockAdjustmentResult(
    InventoryItemDto Item,
    bool IsReplay,
    Guid IdempotencyKey);
