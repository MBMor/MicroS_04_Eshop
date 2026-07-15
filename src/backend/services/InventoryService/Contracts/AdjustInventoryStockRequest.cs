namespace InventoryService.Contracts;

public sealed class AdjustInventoryStockRequest
{
    public int QuantityDelta { get; init; }
}
