using System;

using System.Windows.Controls;

using Eshop.Operations.Desktop.Api.Orders;
using Eshop.Operations.Desktop.ViewModels;

namespace Eshop.Operations.Desktop.Views;

public partial class OrdersView : UserControl
{
    public OrdersView()
    {
        InitializeComponent();
    }

    private async void OrdersDataGrid_CurrentCellChanged(
        object sender,
        EventArgs e)
    {
        if (DataContext is not OrdersViewModel viewModel
            || sender is not DataGrid dataGrid
            || !dataGrid.CurrentCell.IsValid
            || dataGrid.CurrentCell.Item is not OperationalOrderSummaryDto selectedOrder)
        {
            return;
        }

        if (viewModel.SelectedOrder?.Id == selectedOrder.Id)
        {
            return;
        }

        viewModel.SelectedOrder = selectedOrder;
        await viewModel.LoadOrderDetailAsync(selectedOrder);
    }
}
