using System.Windows;

using Eshop.Operations.Desktop.Api.Inventory;
using Eshop.Operations.Desktop.Models;
using Eshop.Operations.Desktop.ViewModels;
using Eshop.Operations.Desktop.Views;

namespace Eshop.Operations.Desktop.Services;

public sealed class InventoryStockAdjustmentDialogService
    : IInventoryStockAdjustmentDialogService
{
    public InventoryStockAdjustmentDraft? ShowConfirmation(InventoryItemDto item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var viewModel = new InventoryStockAdjustmentDialogViewModel(item);
        var dialog = new InventoryStockAdjustmentDialog(viewModel);

        if (Application.Current?.MainWindow is Window owner && owner.IsVisible)
        {
            dialog.Owner = owner;
        }

        bool? result = dialog.ShowDialog();
        return result == true ? dialog.ConfirmedDraft : null;
    }
}
