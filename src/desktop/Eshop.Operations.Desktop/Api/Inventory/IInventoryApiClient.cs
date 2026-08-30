namespace Eshop.Operations.Desktop.Api.Inventory;

public interface IInventoryApiClient
{
    Task<IReadOnlyList<InventoryItemDto>>
        GetInventoryItemsAsync(
            bool includeInactive,
            CancellationToken cancellationToken);

    Task<InventoryStockAdjustmentHistoryPageDto>
        GetStockAdjustmentHistoryAsync(
            Guid inventoryItemId,
            int offset,
            int limit,
            CancellationToken cancellationToken);

    Task<InventoryStockAdjustmentResult>
        AdjustStockAsync(
            InventoryStockAdjustmentRequest request,
            CancellationToken cancellationToken);
}
