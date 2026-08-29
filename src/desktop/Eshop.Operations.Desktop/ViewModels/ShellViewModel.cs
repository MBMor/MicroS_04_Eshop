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
        InventoryViewModel inventory,
        PaymentsViewModel payments,
        DiagnosticsViewModel diagnostics,
        IAuthenticationService authenticationService,
        AuthenticationState authentication)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(authenticationService);
        ArgumentNullException.ThrowIfNull(authentication);

        EnvironmentName =
            options.Value.EnvironmentName;

        Catalog = catalog;
        Inventory = inventory;
        Payments = payments;
        Diagnostics = diagnostics;

        CurrentViewModel = Catalog;

        _authenticationService = authenticationService;

        Authentication = authentication;

        Authentication.PropertyChanged += OnAuthenticationPropertyChanged;

    }

    private readonly IAuthenticationService _authenticationService;

    public AuthenticationState Authentication { get; }

    public CatalogViewModel Catalog { get; }
    public InventoryViewModel Inventory { get; }
    public PaymentsViewModel Payments { get; }

    public bool IsCatalogActive =>
        ReferenceEquals(
            CurrentViewModel,
            Catalog);

    public bool IsInventoryActive =>
        ReferenceEquals(
            CurrentViewModel,
            Inventory);

    public bool IsPaymentsActive =>
        ReferenceEquals(
            CurrentViewModel,
            Payments);

    private bool IsProtectedOperationActive =>
        IsInventoryActive
        || IsPaymentsActive;

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
            InventoryViewModel => "Inventory",
            PaymentsViewModel => "Payments",
            DiagnosticsViewModel => "Diagnostics",
            _ => "Operations"
        };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSectionTitle))]
    [NotifyPropertyChangedFor(nameof(IsCatalogActive))]
    [NotifyPropertyChangedFor(nameof(IsInventoryActive))]
    [NotifyPropertyChangedFor(nameof(IsPaymentsActive))]
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
    private void ShowInventory()
    {
        if (!Authentication.CanAccessOperations)
        {
            StatusText =
                "Sign in with a support or admin account to access Inventory.";

            return;
        }

        CurrentViewModel =
            Inventory;
    }

    [RelayCommand]
    private void ShowPayments()
    {
        if (!Authentication.CanAccessOperations)
        {
            StatusText =
                "Sign in with a support or admin account to access Payments.";

            return;
        }

        CurrentViewModel =
            Payments;
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
    private async Task SignOutAsync(
        CancellationToken cancellationToken)
    {
        StatusText =
            "Signing out...";

        AuthenticationOperationResult result =
            await _authenticationService
                .SignOutAsync(
                    cancellationToken);

        StatusText =
            result.Succeeded
                ? "Signed out."
                : result.ErrorMessage
                    ?? "Signed out locally.";
    }

    private void OnAuthenticationPropertyChanged(
    object? sender,
    System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName
            != nameof(AuthenticationState.CanAccessOperations))
        {
            return;
        }

        if (!Authentication.CanAccessOperations
            && IsProtectedOperationActive)
        {
            CurrentViewModel =
                Catalog;

            StatusText =
                "Operational access is no longer available.";
        }
    }
}
