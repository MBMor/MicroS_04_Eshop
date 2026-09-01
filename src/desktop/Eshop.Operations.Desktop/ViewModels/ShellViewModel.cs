using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eshop.Operations.Desktop.Configuration;
using Microsoft.Extensions.Options;
using Eshop.Operations.Desktop.Authentication;
using Eshop.Operations.Desktop.Navigation;

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
        NotificationsViewModel notifications,
        InvestigationViewModel investigation,
        DiagnosticsViewModel diagnostics,
        IAuthenticationService authenticationService,
        AuthenticationState authentication)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(investigation);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(authenticationService);
        ArgumentNullException.ThrowIfNull(authentication);

        EnvironmentName =
            options.Value.EnvironmentName;

        Catalog = catalog;
        Inventory = inventory;
        Orders = orders;
        Payments = payments;
        Notifications = notifications;
        Investigation = investigation;
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
    public NotificationsViewModel Notifications { get; }

    public bool IsCatalogActive =>
        ReferenceEquals(
            CurrentViewModel,
            Catalog);

    public bool IsInvestigationActive =>
        ReferenceEquals(
            CurrentViewModel,
            Investigation);

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

    public bool IsNotificationsActive =>
        ReferenceEquals(
            CurrentViewModel,
            Notifications);

    private bool IsProtectedOperationActive =>
        IsInventoryActive
        || IsOrdersActive
        || IsPaymentsActive
        || IsNotificationsActive
        || IsInvestigationActive;

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
            NotificationsViewModel => "Notifications",
            InvestigationViewModel => "Investigate",
            DiagnosticsViewModel => "Diagnostics",
            _ => "Operations"
        };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSectionTitle))]
    [NotifyPropertyChangedFor(nameof(IsCatalogActive))]
    [NotifyPropertyChangedFor(nameof(IsInvestigationActive))]
    [NotifyPropertyChangedFor(nameof(IsInventoryActive))]
    [NotifyPropertyChangedFor(nameof(IsOrdersActive))]
    [NotifyPropertyChangedFor(nameof(IsPaymentsActive))]
    [NotifyPropertyChangedFor(nameof(IsNotificationsActive))]
    [NotifyPropertyChangedFor(nameof(IsDiagnosticsActive))]
    public partial object CurrentViewModel { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } =
        "Ready";

    public InvestigationViewModel Investigation { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTroubleshootingContext))]
    [NotifyPropertyChangedFor(nameof(TroubleshootingContextText))]
    public partial TroubleshootingContext? ActiveTroubleshootingContext
    {
        get;
        private set;
    }

    public bool HasTroubleshootingContext =>
        ActiveTroubleshootingContext is not null;

    public string TroubleshootingContextText =>
        ActiveTroubleshootingContext?.DisplayText
        ?? string.Empty;

    private void ClearTroubleshootingContextState()
    {
        TroubleshootingContext? context =
            ActiveTroubleshootingContext;

        if (context is null)
        {
            return;
        }

        switch (context.Kind)
        {
            case TroubleshootingContextKind.OrderToPayments:
                Payments.ClearContextFocus(context.CorrelationId);
                break;

            case TroubleshootingContextKind.ProductToInventory:
                Inventory.ClearContextFocus(context.CorrelationId);
                break;

            case TroubleshootingContextKind.PaymentToOrder:
                Orders.ClearContextFocus(context.CorrelationId);
                break;

            case TroubleshootingContextKind.LookupToOrder:
                Orders.ClearContextFocus(context.CorrelationId);
                break;
        }

        ActiveTroubleshootingContext = null;
    }

    [RelayCommand]
    private void ClearTroubleshootingContext()
    {
        if (ActiveTroubleshootingContext is null)
        {
            return;
        }

        ClearTroubleshootingContextState();
        StatusText = "Troubleshooting context cleared.";
    }

    private void ResetNavigationStatus()
    {
        StatusText = Authentication.IsAuthenticated
            ? $"Signed in as {Authentication.CurrentUser?.DisplayName}."
            : "Ready";
    }

    [RelayCommand]
    private void ShowCatalog()
    {
        ClearTroubleshootingContextState();
        CurrentViewModel = Catalog;
        ResetNavigationStatus();
    }

    [RelayCommand]
    private void ShowInvestigation()
    {
        if (!Authentication.CanAccessOperations)
        {
            StatusText =
                "Sign in with a support or admin account to investigate operational data.";

            return;
        }

        ClearTroubleshootingContextState();

        CurrentViewModel =
            Investigation;

        ResetNavigationStatus();
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

        ClearTroubleshootingContextState();
        CurrentViewModel =
            Inventory;
        ResetNavigationStatus();
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

        ClearTroubleshootingContextState();
        CurrentViewModel =
            Orders;
        ResetNavigationStatus();
    }

    [RelayCommand]
    private async Task OpenOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        if (!Authentication.CanAccessOperations)
        {
            StatusText =
                "Sign in with a support or admin account to access Orders.";

            return;
        }

        if (orderId == Guid.Empty)
        {
            StatusText =
                "A valid order id is required to inspect Orders.";

            return;
        }

        ClearTroubleshootingContextState();

        ActiveTroubleshootingContext =
            new TroubleshootingContext(
                TroubleshootingContextKind.PaymentToOrder,
                orderId);

        CurrentViewModel =
            Orders;

        StatusText =
            $"Inspecting order {orderId:D}.";

        await Orders.FocusOrderAsync(
            orderId,
            cancellationToken);
    }

    [RelayCommand]
    private async Task InspectOperationalIdentifierAsync(
        CancellationToken cancellationToken)
    {
        if (!Authentication.CanAccessOperations)
        {
            StatusText =
                "Sign in with a support or admin account to investigate operational data.";

            return;
        }

        if (!Investigation.TryGetLookup(
                out OperationalLookupKind lookupKind,
                out Guid identifier))
        {
            return;
        }

        switch (lookupKind)
        {
            case OperationalLookupKind.Order:
                await InspectOrderAsync(identifier, cancellationToken);
                break;

            case OperationalLookupKind.PaymentsForOrder:
                await OpenPaymentsForOrderAsync(identifier, cancellationToken);
                break;

            case OperationalLookupKind.InventoryForProduct:
                await OpenInventoryForProductAsync(identifier, cancellationToken);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported lookup kind '{lookupKind}'.");
        }
    }

    private async Task InspectOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        ClearTroubleshootingContextState();

        ActiveTroubleshootingContext =
            new TroubleshootingContext(
                TroubleshootingContextKind.LookupToOrder,
                orderId);

        CurrentViewModel =
            Orders;

        StatusText =
            $"Inspecting order {orderId:D}.";

        await Orders.FocusOrderAsync(
            orderId,
            cancellationToken);
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

        ClearTroubleshootingContextState();

        ActiveTroubleshootingContext =
            new TroubleshootingContext(
                TroubleshootingContextKind.OrderToPayments,
                orderId);

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

        ClearTroubleshootingContextState();
        CurrentViewModel =
            Payments;
        ResetNavigationStatus();
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

        ClearTroubleshootingContextState();

        ActiveTroubleshootingContext =
            new TroubleshootingContext(
                TroubleshootingContextKind.ProductToInventory,
                productId);

        CurrentViewModel = Inventory;
        StatusText = $"Inspecting inventory for product {productId:D}.";
        await Inventory.FocusProductAsync(productId, cancellationToken);
    }

    [RelayCommand]
    private void ShowNotifications()
    {
        if (!Authentication.CanAccessOperations)
        {
            StatusText =
                "Sign in with a support or admin account to access Notifications.";

            return;
        }

        ClearTroubleshootingContextState();

        CurrentViewModel =
            Notifications;

        ResetNavigationStatus();
    }

    [RelayCommand]
    private void ShowDiagnostics()
    {
        ClearTroubleshootingContextState();
        CurrentViewModel = Diagnostics;
        ResetNavigationStatus();
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
            ClearTroubleshootingContextState();

            CurrentViewModel =
                Catalog;

            StatusText =
                "Operational access is no longer available.";
        }
    }
}
