using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Eshop.Operations.Desktop.Navigation;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class InvestigationViewModel : ObservableObject
{
    public InvestigationViewModel()
    {
        SelectedLookupOption = LookupOptions[0];
    }

    public IReadOnlyList<OperationalLookupOption> LookupOptions { get; } =
    [
        new("Order", OperationalLookupKind.Order),
        new("Payments for order", OperationalLookupKind.PaymentsForOrder),
        new("Inventory for product", OperationalLookupKind.InventoryForProduct)
    ];

    [ObservableProperty]
    public partial OperationalLookupOption? SelectedLookupOption { get; set; }

    [ObservableProperty]
    public partial string IdentifierText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool TryGetLookup(
        out OperationalLookupKind lookupKind,
        out Guid identifier)
    {
        ErrorMessage = null;
        lookupKind = default;
        identifier = Guid.Empty;

        if (SelectedLookupOption is null)
        {
            ErrorMessage = "Select a lookup type.";
            return false;
        }

        if (!Guid.TryParse(IdentifierText.Trim(), out identifier)
            || identifier == Guid.Empty)
        {
            ErrorMessage = "Enter a valid non-empty GUID.";
            return false;
        }

        lookupKind = SelectedLookupOption.Kind;
        return true;
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedLookupOption = LookupOptions[0];
        IdentifierText = string.Empty;
        ErrorMessage = null;
    }
}
