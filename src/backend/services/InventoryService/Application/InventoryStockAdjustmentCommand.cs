namespace InventoryService.Application;

public sealed record InventoryStockAdjustmentCommand(
    Guid InventoryItemId,
    int QuantityDelta,
    uint ExpectedVersion,
    string Reason,
    Guid IdempotencyKey,
    string ActorSubject,
    string ActorUsername,
    string? TraceId);
