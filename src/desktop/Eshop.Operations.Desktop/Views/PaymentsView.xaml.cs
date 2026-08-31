using System;

using System.Windows.Controls;

using Eshop.Operations.Desktop.Api.Payments;
using Eshop.Operations.Desktop.ViewModels;

namespace Eshop.Operations.Desktop.Views;

public partial class PaymentsView : UserControl
{
    public PaymentsView()
    {
        InitializeComponent();
    }

    private void PaymentsDataGrid_CurrentCellChanged(
        object sender,
        EventArgs e)
    {
        if (DataContext is not PaymentsViewModel viewModel
            || sender is not DataGrid dataGrid
            || !dataGrid.CurrentCell.IsValid
            || dataGrid.CurrentCell.Item is not PaymentDto payment)
        {
            return;
        }

        viewModel.SelectedPayment = payment;
    }
}
