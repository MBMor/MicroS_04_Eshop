namespace Eshop.Operations.Desktop.Api.Inventory;

public sealed record InventoryStockAdjustmentHistoryItemDto(
    Guid OperationId,
    Guid InventoryItemId,
    Guid? ProductId,
    string? Sku,
    int QuantityDelta,
    long ExpectedVersion,
    string Reason,
    string ActorSubject,
    string ActorUsername,
    string? TraceId,
    string Outcome,
    string? Error,
    int? OnHandBefore,
    int? ReservedBefore,
    int? AvailableBefore,
    int? OnHandAfter,
    int? ReservedAfter,
    int? AvailableAfter,
    long? ResultVersion,
    DateTimeOffset OccurredAtUtc);
