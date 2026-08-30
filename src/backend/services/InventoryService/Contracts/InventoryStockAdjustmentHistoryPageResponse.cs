namespace InventoryService.Contracts;

public sealed record InventoryStockAdjustmentHistoryPageResponse(
    IReadOnlyList<InventoryStockAdjustmentHistoryItemResponse> Items,
    int Offset,
    int Limit,
    bool HasMore);
