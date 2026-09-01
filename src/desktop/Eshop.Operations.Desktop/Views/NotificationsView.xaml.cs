using System;

using System.Windows.Controls;

using Eshop.Operations.Desktop.Api.Notifications;
using Eshop.Operations.Desktop.ViewModels;

namespace Eshop.Operations.Desktop.Views;

public partial class NotificationsView
    : UserControl
{
    public NotificationsView()
    {
        InitializeComponent();
    }

    private void NotificationsDataGrid_CurrentCellChanged(
        object sender,
        EventArgs e)
    {
        if (DataContext
                is not NotificationsViewModel viewModel
            || sender
                is not DataGrid dataGrid
            || !dataGrid.CurrentCell.IsValid
            || dataGrid.CurrentCell.Item
                is not OperationalNotificationDto notification)
        {
            return;
        }

        viewModel.SelectedNotification =
            notification;
    }
}