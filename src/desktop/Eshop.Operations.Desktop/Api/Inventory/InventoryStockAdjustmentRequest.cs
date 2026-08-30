namespace Eshop.Operations.Desktop.Api.Inventory;

public sealed record InventoryStockAdjustmentRequest(
    Guid InventoryItemId,
    int QuantityDelta,
    uint ExpectedVersion,
    string Reason,
    Guid IdempotencyKey);
