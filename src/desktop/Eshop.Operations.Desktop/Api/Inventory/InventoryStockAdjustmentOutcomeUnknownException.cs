namespace Eshop.Operations.Desktop.Api.Inventory;

public sealed class InventoryStockAdjustmentOutcomeUnknownException
    : Exception
{
    public InventoryStockAdjustmentOutcomeUnknownException(
        Guid idempotencyKey,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        if (idempotencyKey == Guid.Empty)
        {
            throw new ArgumentException(
                "Idempotency key must not be empty.",
                nameof(idempotencyKey));
        }

        IdempotencyKey = idempotencyKey;
    }

    public Guid IdempotencyKey { get; }
}
