using System.Windows;
using Eshop.Operations.Desktop.ViewModels;

namespace Eshop.Operations.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        DataContext = viewModel;
    }
}
