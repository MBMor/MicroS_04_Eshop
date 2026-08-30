using InventoryService.Data;

namespace InventoryService.Application;

public sealed record InventoryStockAdjustmentExecutionResult(
    InventoryMutationStatus Status,
    InventoryStockAdjustmentOperation? Operation,
    string? Error,
    bool IsReplay)
{
    public static InventoryStockAdjustmentExecutionResult FromOperation(
        InventoryStockAdjustmentOperation operation,
        bool isReplay)
    {
        ArgumentNullException.ThrowIfNull(operation);

        InventoryMutationStatus status = operation.Outcome switch
        {
            InventoryStockAdjustmentOutcome.Success =>
                InventoryMutationStatus.Success,
            InventoryStockAdjustmentOutcome.NotFound =>
                InventoryMutationStatus.NotFound,
            InventoryStockAdjustmentOutcome.Conflict =>
                InventoryMutationStatus.Conflict,
            InventoryStockAdjustmentOutcome.ValidationFailed =>
                InventoryMutationStatus.ValidationFailed,
            InventoryStockAdjustmentOutcome.Pending =>
                throw new InvalidOperationException(
                    "A pending stock adjustment cannot be returned as a completed result."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation.Outcome,
                "Unknown stock adjustment outcome.")
        };

        return new InventoryStockAdjustmentExecutionResult(
            status,
            operation,
            operation.Error,
            isReplay);
    }

    public static InventoryStockAdjustmentExecutionResult Conflict(
        string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new InventoryStockAdjustmentExecutionResult(
            InventoryMutationStatus.Conflict,
            null,
            error,
            false);
    }
}
