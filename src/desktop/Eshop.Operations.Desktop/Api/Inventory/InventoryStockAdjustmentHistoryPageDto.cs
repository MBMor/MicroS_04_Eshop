namespace Eshop.Operations.Desktop.Api.Inventory;

public sealed record InventoryStockAdjustmentHistoryPageDto(
    IReadOnlyList<InventoryStockAdjustmentHistoryItemDto> Items,
    int Offset,
    int Limit,
    bool HasMore);
