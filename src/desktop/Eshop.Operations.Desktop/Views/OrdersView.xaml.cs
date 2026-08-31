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

    private async void OrdersDataGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (DataContext is not OrdersViewModel viewModel
            || sender is not DataGrid dataGrid
            || dataGrid.SelectedItem is not OperationalOrderSummaryDto selectedOrder)
        {
            return;
        }

        await viewModel.LoadOrderDetailAsync(selectedOrder);
    }
}
