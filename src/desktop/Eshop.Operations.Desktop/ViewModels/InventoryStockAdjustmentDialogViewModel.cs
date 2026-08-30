using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Eshop.Operations.Desktop.Api.Inventory;
using Eshop.Operations.Desktop.Models;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class InventoryStockAdjustmentDialogViewModel
    : ObservableObject
{
    public InventoryStockAdjustmentDialogViewModel(
        InventoryItemDto item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Item = item;
    }

    public InventoryItemDto Item { get; }

    public int? QuantityDelta =>
        TryGetQuantityDelta(out int quantityDelta) ? quantityDelta : null;

    public int? ProjectedOnHandQuantity =>
        TryGetProjectedOnHandQuantity(out int projectedQuantity)
            ? projectedQuantity
            : null;

    public int? ProjectedAvailableQuantity
    {
        get
        {
            if (!TryGetProjectedOnHandQuantity(out int projectedOnHand))
            {
                return null;
            }

            return projectedOnHand - Item.ReservedQuantity;
        }
    }

    public string? ValidationMessage => GetValidationMessage();

    public bool CanApply => ValidationMessage is null;

    [ObservableProperty]
    public partial string QuantityDeltaText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    partial void OnQuantityDeltaTextChanged(string value)
    {
        OnPropertyChanged(nameof(QuantityDelta));
        OnPropertyChanged(nameof(ProjectedOnHandQuantity));
        OnPropertyChanged(nameof(ProjectedAvailableQuantity));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(CanApply));
    }

    partial void OnReasonChanged(string value)
    {
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(CanApply));
    }

    public bool TryCreateDraft(out InventoryStockAdjustmentDraft? draft)
    {
        draft = null;
        if (ValidationMessage is not null || !TryGetQuantityDelta(out int quantityDelta))
        {
            return false;
        }

        draft = new InventoryStockAdjustmentDraft(quantityDelta, Reason.Trim());
        return true;
    }

    private string? GetValidationMessage()
    {
        if (string.IsNullOrWhiteSpace(QuantityDeltaText))
        {
            return "Enter a quantity delta.";
        }

        if (!TryGetQuantityDelta(out int quantityDelta))
        {
            return "Quantity delta must be a whole number.";
        }

        if (quantityDelta == 0)
        {
            return "Quantity delta must not be zero.";
        }

        long projectedOnHand = (long)Item.OnHandQuantity + quantityDelta;
        if (projectedOnHand > int.MaxValue || projectedOnHand < int.MinValue)
        {
            return "The resulting on-hand quantity is outside the supported range.";
        }

        if (projectedOnHand < 0)
        {
            return "The resulting on-hand quantity must not be negative.";
        }

        if (projectedOnHand < Item.ReservedQuantity)
        {
            return $"The resulting on-hand quantity cannot be lower than the reserved quantity {Item.ReservedQuantity}.";
        }

        string reason = Reason.Trim();
        if (reason.Length < 3)
        {
            return "Reason must contain at least 3 characters.";
        }

        if (reason.Length > 500)
        {
            return "Reason must not exceed 500 characters.";
        }

        return null;
    }

    private bool TryGetQuantityDelta(out int quantityDelta) =>
        int.TryParse(QuantityDeltaText, NumberStyles.Integer, CultureInfo.CurrentCulture, out quantityDelta);

    private bool TryGetProjectedOnHandQuantity(out int projectedQuantity)
    {
        projectedQuantity = default;
        if (!TryGetQuantityDelta(out int quantityDelta))
        {
            return false;
        }

        long projected = (long)Item.OnHandQuantity + quantityDelta;
        if (projected > int.MaxValue || projected < int.MinValue)
        {
            return false;
        }

        projectedQuantity = (int)projected;
        return true;
    }
}
