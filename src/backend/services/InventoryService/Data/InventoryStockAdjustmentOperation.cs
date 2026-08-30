using InventoryService.Domain;

namespace InventoryService.Data;

public sealed class InventoryStockAdjustmentOperation
{
    private InventoryStockAdjustmentOperation()
    {
    }

    private InventoryStockAdjustmentOperation(
        Guid id,
        Guid idempotencyKey,
        Guid inventoryItemId,
        int quantityDelta,
        uint expectedVersion,
        string reason,
        string actorSubject,
        string actorUsername,
        string? traceId,
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        IdempotencyKey = idempotencyKey;
        InventoryItemId = inventoryItemId;
        QuantityDelta = quantityDelta;
        ExpectedVersion = expectedVersion;
        Reason = reason;
        ActorSubject = actorSubject;
        ActorUsername = actorUsername;
        TraceId = traceId;
        OccurredAtUtc = occurredAtUtc;
        Outcome = InventoryStockAdjustmentOutcome.Pending;
    }

    public Guid Id { get; private set; }

    public Guid IdempotencyKey { get; private set; }

    public Guid InventoryItemId { get; private set; }

    public int QuantityDelta { get; private set; }

    public long ExpectedVersion { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string ActorSubject { get; private set; } = string.Empty;

    public string ActorUsername { get; private set; } = string.Empty;

    public string? TraceId { get; private set; }

    public InventoryStockAdjustmentOutcome Outcome { get; private set; }

    public string? Error { get; private set; }

    public Guid? ProductId { get; private set; }

    public string? Sku { get; private set; }

    public int? OnHandBefore { get; private set; }

    public int? ReservedBefore { get; private set; }

    public int? AvailableBefore { get; private set; }

    public int? OnHandAfter { get; private set; }

    public int? ReservedAfter { get; private set; }

    public int? AvailableAfter { get; private set; }

    public bool? IsActive { get; private set; }

    public DateTimeOffset? ItemCreatedAtUtc { get; private set; }

    public DateTimeOffset? ItemUpdatedAtUtc { get; private set; }

    public long? ResultVersion { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static InventoryStockAdjustmentOperation Begin(
        Guid idempotencyKey,
        Guid inventoryItemId,
        int quantityDelta,
        uint expectedVersion,
        string reason,
        string actorSubject,
        string actorUsername,
        string? traceId,
        DateTimeOffset occurredAtUtc)
    {
        if (idempotencyKey == Guid.Empty)
        {
            throw new ArgumentException(
                "Idempotency key must not be empty.",
                nameof(idempotencyKey));
        }

        if (inventoryItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "Inventory item id must not be empty.",
                nameof(inventoryItemId));
        }

        if (quantityDelta == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantityDelta),
                quantityDelta,
                "Quantity delta must not be zero.");
        }

        if (expectedVersion == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion),
                expectedVersion,
                "Expected version must be greater than zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUsername);

        return new InventoryStockAdjustmentOperation(
            Guid.NewGuid(),
            idempotencyKey,
            inventoryItemId,
            quantityDelta,
            expectedVersion,
            reason,
            actorSubject,
            actorUsername,
            traceId,
            occurredAtUtc);
    }

    public bool MatchesRequest(
        Guid inventoryItemId,
        int quantityDelta,
        uint expectedVersion,
        string reason,
        string actorSubject)
    {
        return InventoryItemId == inventoryItemId
            && QuantityDelta == quantityDelta
            && ExpectedVersion == expectedVersion
            && string.Equals(
                Reason,
                reason,
                StringComparison.Ordinal)
            && string.Equals(
                ActorSubject,
                actorSubject,
                StringComparison.Ordinal);
    }

    public void CompleteSuccess(
        InventoryItem inventoryItem,
        int onHandBefore,
        int reservedBefore,
        int availableBefore)
    {
        ArgumentNullException.ThrowIfNull(inventoryItem);
        EnsurePending();

        ProductId = inventoryItem.ProductId;
        Sku = inventoryItem.Sku;
        OnHandBefore = onHandBefore;
        ReservedBefore = reservedBefore;
        AvailableBefore = availableBefore;
        OnHandAfter = inventoryItem.OnHandQuantity;
        ReservedAfter = inventoryItem.ReservedQuantity;
        AvailableAfter = inventoryItem.AvailableQuantity;
        IsActive = inventoryItem.IsActive;
        ItemCreatedAtUtc = inventoryItem.CreatedAtUtc;
        ItemUpdatedAtUtc = inventoryItem.UpdatedAtUtc;
        ResultVersion = inventoryItem.Version;
        Outcome = InventoryStockAdjustmentOutcome.Success;
    }

    public void CompleteFailure(
        InventoryStockAdjustmentOutcome outcome,
        string error)
    {
        EnsurePending();

        if (outcome is not (
            InventoryStockAdjustmentOutcome.NotFound
            or InventoryStockAdjustmentOutcome.Conflict
            or InventoryStockAdjustmentOutcome.ValidationFailed))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "The supplied outcome is not a failure outcome.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        Error = error;
        Outcome = outcome;
    }

    private void EnsurePending()
    {
        if (Outcome != InventoryStockAdjustmentOutcome.Pending)
        {
            throw new InvalidOperationException(
                "The stock adjustment operation is already completed.");
        }
    }
}
