using CommunityToolkit.Mvvm.ComponentModel;
using Eshop.Operations.Desktop.Configuration;
using Microsoft.Extensions.Options;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly string _applicationTitle = "Eshop Operations Console";

    public ShellViewModel(IOptions<DesktopOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        EnvironmentName = options.Value.EnvironmentName;
    }

    public string ApplicationTitle => _applicationTitle;

    public string EnvironmentName { get; }

    public string WindowTitle =>
        $"{ApplicationTitle} — {EnvironmentName}";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready";
}
