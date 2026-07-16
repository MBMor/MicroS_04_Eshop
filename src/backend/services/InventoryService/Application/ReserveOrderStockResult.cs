namespace InventoryService.Application;

public enum ReserveOrderStockStatus
{
    Reserved,
    Failed
}

public sealed record ReserveOrderStockResult(
    ReserveOrderStockStatus Status,
    string? FailureReason);
