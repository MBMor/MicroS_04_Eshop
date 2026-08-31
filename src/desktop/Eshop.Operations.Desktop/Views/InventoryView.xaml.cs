using System;

using System.Windows.Controls;

using Eshop.Operations.Desktop.Api.Inventory;
using Eshop.Operations.Desktop.ViewModels;

namespace Eshop.Operations.Desktop.Views;

public partial class InventoryView : UserControl
{
    public InventoryView()
    {
        InitializeComponent();
    }

    private void InventoryDataGrid_CurrentCellChanged(
        object sender,
        EventArgs e)
    {
        if (DataContext is not InventoryViewModel viewModel
            || sender is not DataGrid dataGrid
            || !dataGrid.CurrentCell.IsValid
            || dataGrid.CurrentCell.Item is not InventoryItemDto item)
        {
            return;
        }

        viewModel.SelectedItem = item;
    }

    private void AdjustmentHistoryDataGrid_CurrentCellChanged(
        object sender,
        EventArgs e)
    {
        if (DataContext is not InventoryViewModel viewModel
            || sender is not DataGrid dataGrid
            || !dataGrid.CurrentCell.IsValid
            || dataGrid.CurrentCell.Item is not InventoryStockAdjustmentHistoryItemDto historyItem)
        {
            return;
        }

        viewModel.SelectedHistoryItem = historyItem;
    }
}
