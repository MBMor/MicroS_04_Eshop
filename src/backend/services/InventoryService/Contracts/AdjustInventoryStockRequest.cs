namespace InventoryService.Contracts;

public sealed class AdjustInventoryStockRequest
{
    public int QuantityDelta { get; init; }

    public uint ExpectedVersion { get; init; }

    public string Reason { get; init; } = string.Empty;
}
