using System;

using System.Windows.Controls;

using Eshop.Operations.Desktop.Api.Catalog;
using Eshop.Operations.Desktop.ViewModels;

namespace Eshop.Operations.Desktop.Views;

public partial class CatalogView : UserControl
{
    public CatalogView()
    {
        InitializeComponent();
    }

    private void CatalogDataGrid_CurrentCellChanged(
        object sender,
        EventArgs e)
    {
        if (DataContext is not CatalogViewModel viewModel
            || sender is not DataGrid dataGrid
            || !dataGrid.CurrentCell.IsValid
            || dataGrid.CurrentCell.Item is not CatalogProductDto product)
        {
            return;
        }

        viewModel.SelectedProduct = product;
    }
}
