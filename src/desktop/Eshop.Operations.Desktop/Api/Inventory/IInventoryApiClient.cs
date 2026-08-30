namespace Eshop.Operations.Desktop.Api.Inventory;

public interface IInventoryApiClient
{
    Task<IReadOnlyList<InventoryItemDto>>
        GetInventoryItemsAsync(
            bool includeInactive,
            CancellationToken cancellationToken);

    Task<InventoryStockAdjustmentResult>
        AdjustStockAsync(
            InventoryStockAdjustmentRequest request,
            CancellationToken cancellationToken);
}
