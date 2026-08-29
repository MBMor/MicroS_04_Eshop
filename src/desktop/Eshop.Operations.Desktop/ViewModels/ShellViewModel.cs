using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eshop.Operations.Desktop.Configuration;
using Microsoft.Extensions.Options;
using Eshop.Operations.Desktop.Authentication;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly string _applicationTitle =
        "Eshop Operations Console";

    public ShellViewModel(
        IOptions<DesktopOptions> options,
        CatalogViewModel catalog,
        DiagnosticsViewModel diagnostics,
        IAuthenticationService authenticationService,
        AuthenticationState authentication)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(authenticationService);
        ArgumentNullException.ThrowIfNull(authentication);

        EnvironmentName =
            options.Value.EnvironmentName;

        Catalog = catalog;
        Diagnostics = diagnostics;

        CurrentViewModel = Catalog;

        _authenticationService = authenticationService;

        Authentication = authentication;

    }

    private readonly IAuthenticationService _authenticationService;

    public AuthenticationState Authentication { get; }

    public CatalogViewModel Catalog { get; }

    public bool IsCatalogActive =>
        ReferenceEquals(
            CurrentViewModel,
            Catalog);

    public bool IsDiagnosticsActive =>
        ReferenceEquals(
            CurrentViewModel,
            Diagnostics);

    public DiagnosticsViewModel Diagnostics { get; }

    public string ApplicationTitle =>
        _applicationTitle;

    public string EnvironmentName { get; }

    public string WindowTitle =>
        $"{ApplicationTitle} — {EnvironmentName}";

    public string CurrentSectionTitle =>
        CurrentViewModel switch
        {
            CatalogViewModel => "Catalog",
            DiagnosticsViewModel => "Diagnostics",
            _ => "Operations"
        };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSectionTitle))]
    [NotifyPropertyChangedFor(nameof(IsCatalogActive))]
    [NotifyPropertyChangedFor(nameof(IsDiagnosticsActive))]
    public partial object CurrentViewModel { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } =
        "Ready";

    [RelayCommand]
    private void ShowCatalog()
    {
        CurrentViewModel = Catalog;
    }

    [RelayCommand]
    private void ShowDiagnostics()
    {
        CurrentViewModel = Diagnostics;
    }

    [RelayCommand]
    private async Task SignInAsync(
    CancellationToken cancellationToken)
    {
        StatusText =
            "Signing in...";

        AuthenticationOperationResult result =
            await _authenticationService.SignInAsync(
                cancellationToken);

        StatusText =
            result.Succeeded
                ? $"Signed in as {Authentication.CurrentUser?.DisplayName}."
                : result.ErrorMessage
                    ?? "Authentication failed.";
    }

    [RelayCommand]
    private void SignOut()
    {
        _authenticationService.SignOut();

        StatusText =
            "Signed out.";
    }
}
