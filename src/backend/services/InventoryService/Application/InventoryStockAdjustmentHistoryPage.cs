using InventoryService.Data;

namespace InventoryService.Application;

public sealed record InventoryStockAdjustmentHistoryPage(
    IReadOnlyList<InventoryStockAdjustmentOperation> Items,
    int Offset,
    int Limit,
    bool HasMore);
