using System.Windows;

using Eshop.Operations.Desktop.Models;
using Eshop.Operations.Desktop.ViewModels;

namespace Eshop.Operations.Desktop.Views;

public partial class InventoryStockAdjustmentDialog : Window
{
    public InventoryStockAdjustmentDialog(InventoryStockAdjustmentDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
    }

    public InventoryStockAdjustmentDraft? ConfirmedDraft { get; private set; }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not InventoryStockAdjustmentDialogViewModel viewModel
            || !viewModel.TryCreateDraft(out InventoryStockAdjustmentDraft? draft)
            || draft is null)
        {
            return;
        }

        ConfirmedDraft = draft;
        DialogResult = true;
    }
}
