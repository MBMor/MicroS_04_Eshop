namespace InventoryService.Domain;

public sealed class InventoryItem
{
    private InventoryItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public int OnHandQuantity { get; private set; }

    public int ReservedQuantity { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public int AvailableQuantity =>
        OnHandQuantity - ReservedQuantity;

    public static InventoryItem Create(
        Guid id,
        Guid productId,
        string sku,
        int initialOnHandQuantity,
        bool isActive,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Inventory item id must not be empty.",
                nameof(id));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException(
                "Product id must not be empty.",
                nameof(productId));
        }

        if (initialOnHandQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialOnHandQuantity),
                initialOnHandQuantity,
                "Initial on-hand quantity must not be negative.");
        }

        return new InventoryItem
        {
            Id = id,
            ProductId = productId,
            Sku = NormalizeSku(sku),
            OnHandQuantity = initialOnHandQuantity,
            ReservedQuantity = 0,
            IsActive = isActive,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = null
        };
    }

    public void Update(
        string sku,
        int onHandQuantity,
        bool isActive,
        DateTimeOffset updatedAtUtc)
    {
        ValidateOnHandQuantity(onHandQuantity);

        Sku = NormalizeSku(sku);
        OnHandQuantity = onHandQuantity;
        IsActive = isActive;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void AdjustOnHandQuantity(
        int quantityDelta,
        DateTimeOffset updatedAtUtc)
    {
        if (quantityDelta == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantityDelta),
                quantityDelta,
                "Stock adjustment must not be zero.");
        }

        int adjustedQuantity;

        try
        {
            adjustedQuantity = checked(
                OnHandQuantity + quantityDelta);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException(
                "The resulting stock quantity is outside the supported range.",
                nameof(quantityDelta),
                exception);
        }

        ValidateOnHandQuantity(adjustedQuantity);

        OnHandQuantity = adjustedQuantity;
        UpdatedAtUtc = updatedAtUtc;
    }

    public bool TryReserve(
        int quantity,
        DateTimeOffset updatedAtUtc)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "Reservation quantity must be greater than zero.");
        }

        if (!IsActive || AvailableQuantity < quantity)
        {
            return false;
        }

        ReservedQuantity = checked(
            ReservedQuantity + quantity);

        UpdatedAtUtc = updatedAtUtc;

        return true;
    }

    public void ReleaseReservation(
        int quantity,
        DateTimeOffset updatedAtUtc)
    {
        ValidateReservedQuantityReduction(quantity);

        ReservedQuantity -= quantity;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void CommitReservation(
        int quantity,
        DateTimeOffset updatedAtUtc)
    {
        ValidateReservedQuantityReduction(quantity);

        ReservedQuantity -= quantity;
        OnHandQuantity -= quantity;
        UpdatedAtUtc = updatedAtUtc;
    }

    private void ValidateOnHandQuantity(int onHandQuantity)
    {
        if (onHandQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(onHandQuantity),
                onHandQuantity,
                "On-hand quantity must not be negative.");
        }

        if (onHandQuantity < ReservedQuantity)
        {
            throw new InvalidOperationException(
                $"On-hand quantity cannot be lower than reserved quantity {ReservedQuantity}.");
        }
    }

    private void ValidateReservedQuantityReduction(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "Quantity must be greater than zero.");
        }

        if (quantity > ReservedQuantity)
        {
            throw new InvalidOperationException(
                $"Cannot reduce reservation by {quantity}; only {ReservedQuantity} units are reserved.");
        }
    }

    private static string NormalizeSku(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException(
                "SKU must not be empty.",
                nameof(sku));
        }

        string normalizedSku = sku.Trim().ToUpperInvariant();

        if (normalizedSku.Length > 64)
        {
            throw new ArgumentException(
                "SKU must not exceed 64 characters.",
                nameof(sku));
        }

        return normalizedSku;
    }
}
