using InventoryService.Data;

namespace InventoryService.Contracts;

public sealed record InventoryStockAdjustmentHistoryItemResponse(
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
    DateTimeOffset OccurredAtUtc)
{
    public static InventoryStockAdjustmentHistoryItemResponse FromOperation(
        InventoryStockAdjustmentOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return new InventoryStockAdjustmentHistoryItemResponse(
            operation.Id,
            operation.InventoryItemId,
            operation.ProductId,
            operation.Sku,
            operation.QuantityDelta,
            operation.ExpectedVersion,
            operation.Reason,
            operation.ActorSubject,
            operation.ActorUsername,
            operation.TraceId,
            operation.Outcome.ToString(),
            operation.Error,
            operation.OnHandBefore,
            operation.ReservedBefore,
            operation.AvailableBefore,
            operation.OnHandAfter,
            operation.ReservedAfter,
            operation.AvailableAfter,
            operation.ResultVersion,
            operation.OccurredAtUtc);
    }
}
