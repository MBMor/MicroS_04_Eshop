namespace Eshop.Operations.Desktop.Models;

public sealed record InventoryStockAdjustmentDraft(
    int QuantityDelta,
    string Reason);
