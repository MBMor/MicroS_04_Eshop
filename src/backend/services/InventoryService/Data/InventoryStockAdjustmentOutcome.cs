namespace InventoryService.Data;

public enum InventoryStockAdjustmentOutcome
{
    Pending,
    Success,
    NotFound,
    Conflict,
    ValidationFailed
}
