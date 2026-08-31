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
        OrdersViewModel orders,
        PaymentsViewModel payments,
        DiagnosticsViewModel diagnostics,
        IAuthenticationService authenticationService,
        AuthenticationState authentication)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(authenticationService);
        ArgumentNullException.ThrowIfNull(authentication);

        EnvironmentName =
            options.Value.EnvironmentName;

        Catalog = catalog;
        Inventory = inventory;
        Orders = orders;
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
    public OrdersViewModel Orders { get; }
    public PaymentsViewModel Payments { get; }

    public bool IsCatalogActive =>
        ReferenceEquals(
            CurrentViewModel,
            Catalog);

    public bool IsInventoryActive =>
        ReferenceEquals(
            CurrentViewModel,
            Inventory);

    public bool IsOrdersActive =>
        ReferenceEquals(
            CurrentViewModel,
            Orders);

    public bool IsPaymentsActive =>
        ReferenceEquals(
            CurrentViewModel,
            Payments);

    private bool IsProtectedOperationActive =>
        IsInventoryActive
        || IsOrdersActive
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
            OrdersViewModel => "Orders",
            PaymentsViewModel => "Payments",
            DiagnosticsViewModel => "Diagnostics",
            _ => "Operations"
        };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSectionTitle))]
    [NotifyPropertyChangedFor(nameof(IsCatalogActive))]
    [NotifyPropertyChangedFor(nameof(IsInventoryActive))]
    [NotifyPropertyChangedFor(nameof(IsOrdersActive))]
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
    private void ShowOrders()
    {
        if (!Authentication.CanAccessOperations)
        {
            StatusText =
                "Sign in with a support or admin account to access Orders.";

            return;
        }

        CurrentViewModel =
            Orders;
    }

    [RelayCommand]
    private async Task OpenPaymentsForOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        if (!Authentication.CanAccessOperations)
        {
            StatusText =
                "Sign in with a support or admin account to access Payments.";
            return;
        }

        if (orderId == Guid.Empty)
        {
            StatusText =
                "A valid order id is required to inspect Payments.";
            return;
        }

        CurrentViewModel = Payments;
        StatusText = $"Inspecting payments for order {orderId:D}.";
        await Payments.FocusOrderAsync(orderId, cancellationToken);
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
    private async Task OpenInventoryForProductAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (!Authentication.CanAccessOperations)
        {
            StatusText =
                "Sign in with a support or admin account to access Inventory.";
            return;
        }

        if (productId == Guid.Empty)
        {
            StatusText =
                "A valid product id is required to inspect Inventory.";
            return;
        }

        CurrentViewModel = Inventory;
        StatusText = $"Inspecting inventory for product {productId:D}.";
        await Inventory.FocusProductAsync(productId, cancellationToken);
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
