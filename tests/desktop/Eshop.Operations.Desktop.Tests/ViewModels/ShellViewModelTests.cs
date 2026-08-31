using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Catalog;
using Eshop.Operations.Desktop.Api.Inventory;
using Eshop.Operations.Desktop.Api.Payments;
using Eshop.Operations.Desktop.Api.Orders;
using Eshop.Operations.Desktop.Authentication;
using Eshop.Operations.Desktop.Configuration;
using Eshop.Operations.Desktop.Models;
using Eshop.Operations.Desktop.Navigation;
using Eshop.Operations.Desktop.Services;
using Eshop.Operations.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Eshop.Operations.Desktop.Tests.ViewModels;

public sealed class ShellViewModelTests
{
    [Fact]
    public void ConstructorInitializesShellPresentationState()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(
            "Eshop Operations Console",
            viewModel.ApplicationTitle);

        Assert.Equal(
            "Local",
            viewModel.EnvironmentName);

        Assert.Equal(
            "Eshop Operations Console — Local",
            viewModel.WindowTitle);

        Assert.Equal(
            "Ready",
            viewModel.StatusText);
    }

    [Fact]
    public void StatusTextWhenChangedRaisesPropertyChanged()
    {
        var viewModel = CreateViewModel();

        string? changedPropertyName = null;

        viewModel.PropertyChanged += (_, args) =>
        {
            changedPropertyName = args.PropertyName;
        };

        viewModel.StatusText = "Working";

        Assert.Equal(
            nameof(ShellViewModel.StatusText),
            changedPropertyName);

        Assert.Equal(
            "Working",
            viewModel.StatusText);
    }

    [Fact]
    public void StatusTextWhenAssignedSameValueDoesNotRaisePropertyChanged()
    {
        var viewModel = CreateViewModel();

        var notificationCount = 0;

        viewModel.PropertyChanged += (_, _) =>
        {
            notificationCount++;
        };

        viewModel.StatusText = "Ready";

        Assert.Equal(
            0,
            notificationCount);
    }

    [Fact]
    public void ConstructorSelectsCatalogAsInitialDestination()
    {
        ShellViewModel viewModel =
            CreateViewModel();

        Assert.Same(
            viewModel.Catalog,
            viewModel.CurrentViewModel);

        Assert.Equal(
            "Catalog",
            viewModel.CurrentSectionTitle);
    }

    [Fact]
    public void ShowDiagnosticsCommandNavigatesToDiagnostics()
    {
        ShellViewModel viewModel =
            CreateViewModel();

        viewModel.ShowDiagnosticsCommand.Execute(null);

        Assert.Same(
            viewModel.Diagnostics,
            viewModel.CurrentViewModel);

        Assert.Equal(
            "Diagnostics",
            viewModel.CurrentSectionTitle);
    }

    [Fact]
    public void ShowCatalogCommandNavigatesBackToCatalog()
    {
        ShellViewModel viewModel =
            CreateViewModel();

        viewModel.ShowDiagnosticsCommand.Execute(null);

        viewModel.ShowCatalogCommand.Execute(null);

        Assert.Same(
            viewModel.Catalog,
            viewModel.CurrentViewModel);
    }

    [Fact]
    public void NavigationKeepsExistingCatalogViewModelInstance()
    {
        ShellViewModel viewModel =
            CreateViewModel();

        CatalogViewModel catalog =
            viewModel.Catalog;

        catalog.SearchText =
            "keyboard";

        viewModel.ShowDiagnosticsCommand.Execute(null);
        viewModel.ShowCatalogCommand.Execute(null);

        Assert.Same(
            catalog,
            viewModel.CurrentViewModel);

        Assert.Equal(
            "keyboard",
            catalog.SearchText);
    }

    [Fact]
    public void NavigationUpdatesActiveDestinationState()
    {
        ShellViewModel viewModel =
            CreateViewModel();

        Assert.True(
            viewModel.IsCatalogActive);

        Assert.False(
            viewModel.IsDiagnosticsActive);

        viewModel.ShowDiagnosticsCommand.Execute(null);

        Assert.False(
            viewModel.IsCatalogActive);

        Assert.True(
            viewModel.IsDiagnosticsActive);

        viewModel.ShowCatalogCommand.Execute(null);

        Assert.True(
            viewModel.IsCatalogActive);

        Assert.False(
            viewModel.IsDiagnosticsActive);
    }

    [Fact]
    public void ShowInventoryCommandDoesNotNavigateWithoutOperationalRole()
    {
        ShellViewModel viewModel =
            CreateViewModel();

        viewModel.ShowInventoryCommand.Execute(null);

        Assert.Same(
            viewModel.Catalog,
            viewModel.CurrentViewModel);
    }

    [Fact]
    public void SupportRoleCanAccessOperations()
    {
        var authentication =
            new AuthenticationState(
                new AuthenticatedUser(
                    "user-123",
                    "sam.support",
                    "sam.support@eshop.local",
                    ["support"]));

        Assert.True(
            authentication.CanAccessOperations);
    }

    [Fact]
    public void CustomerRoleCannotAccessOperations()
    {
        var authentication =
            new AuthenticationState(
                new AuthenticatedUser(
                    "user-123",
                    "sam.customer",
                    "sam.customer@eshop.local",
                    ["customer"]));

        Assert.False(
            authentication.CanAccessOperations);
    }

    [Fact]
    public void ShowPaymentsCommandDoesNotNavigateWithoutOperationalRole()
    {
        ShellViewModel viewModel =
            CreateViewModel();

        viewModel.ShowPaymentsCommand.Execute(null);

        Assert.Same(
            viewModel.Catalog,
            viewModel.CurrentViewModel);

        Assert.Equal(
            "Sign in with a support or admin account to access Payments.",
            viewModel.StatusText);
    }

    [Fact]
    public void ShowOrdersCommandDoesNotNavigateWithoutOperationalRole()
    {
        ShellViewModel viewModel = CreateViewModel();

        viewModel.ShowOrdersCommand.Execute(null);

        Assert.Same(viewModel.Catalog, viewModel.CurrentViewModel);
        Assert.Equal(
            "Sign in with a support or admin account to access Orders.",
            viewModel.StatusText);
    }

    [Fact]
    public void ShowOrdersCommandNavigatesForSupportUser()
    {
        var authentication = new AuthenticationState(
            new AuthenticatedUser(
                "support-123",
                "sam.support",
                "sam.support@example.com",
                ["support"]));

        ShellViewModel viewModel = CreateViewModel(authentication);

        viewModel.ShowOrdersCommand.Execute(null);

        Assert.Same(viewModel.Orders, viewModel.CurrentViewModel);
        Assert.True(viewModel.IsOrdersActive);
        Assert.Equal("Orders", viewModel.CurrentSectionTitle);
    }

    [Fact]
    public void LosingOperationalAccessWhileOrdersActiveReturnsToCatalog()
    {
        var authentication = new AuthenticationState(
            new AuthenticatedUser(
                "support-123",
                "sam.support",
                "sam.support@example.com",
                ["support"]));

        ShellViewModel viewModel = CreateViewModel(authentication);
        viewModel.ShowOrdersCommand.Execute(null);

        Assert.Same(viewModel.Orders, viewModel.CurrentViewModel);

        authentication.CurrentUser = new AuthenticatedUser(
            "customer-123",
            "sam.customer",
            "sam.customer@example.com",
            ["customer"]);

        Assert.Same(viewModel.Catalog, viewModel.CurrentViewModel);
        Assert.Equal(
            "Operational access is no longer available.",
            viewModel.StatusText);
    }

    [Fact]
    public async Task OpenPaymentsForOrderCommandFocusesPaymentsForSupportUser()
    {
        var authentication = new AuthenticationState(
            new AuthenticatedUser(
                "support-123",
                "sam.support",
                "sam.support@example.com",
                ["support"]));

        ShellViewModel viewModel = CreateViewModel(authentication);
        Guid orderId = Guid.NewGuid();

        await viewModel.OpenPaymentsForOrderCommand.ExecuteAsync(orderId);

        Assert.Same(viewModel.Payments, viewModel.CurrentViewModel);
        Assert.Equal(orderId.ToString("D"), viewModel.Payments.SearchText);
        Assert.True(viewModel.HasTroubleshootingContext);
        Assert.NotNull(viewModel.ActiveTroubleshootingContext);
        Assert.Equal(
            TroubleshootingContextKind.OrderToPayments,
            viewModel.ActiveTroubleshootingContext.Kind);
        Assert.Equal(orderId, viewModel.ActiveTroubleshootingContext.CorrelationId);
        Assert.Equal(
            $"Order {orderId.ToString("D")[..8]}… → Payments",
            viewModel.TroubleshootingContextText);
    }

    [Fact]
    public async Task OpenInventoryForProductCommandFocusesInventoryForSupportUser()
    {
        var authentication = new AuthenticationState(
            new AuthenticatedUser(
                "support-123",
                "sam.support",
                "sam.support@example.com",
                ["support"]));

        ShellViewModel viewModel = CreateViewModel(authentication);
        Guid productId = Guid.NewGuid();

        await viewModel.OpenInventoryForProductCommand.ExecuteAsync(productId);

        Assert.Same(viewModel.Inventory, viewModel.CurrentViewModel);
        Assert.Equal(productId.ToString("D"), viewModel.Inventory.SearchText);
        Assert.Equal(
            TroubleshootingContextKind.ProductToInventory,
            viewModel.ActiveTroubleshootingContext?.Kind);
        Assert.Equal(
            productId,
            viewModel.ActiveTroubleshootingContext?.CorrelationId);
    }

    [Fact]
    public async Task ContextualNavigationIsBlockedWithoutOperationalRole()
    {
        ShellViewModel viewModel = CreateViewModel();
        Guid orderId = Guid.NewGuid();

        await viewModel.OpenPaymentsForOrderCommand.ExecuteAsync(orderId);

        Assert.Same(viewModel.Catalog, viewModel.CurrentViewModel);
        Assert.Equal(
            "Sign in with a support or admin account to access Payments.",
            viewModel.StatusText);

        await viewModel.OpenOrderCommand.ExecuteAsync(orderId);

        Assert.Same(viewModel.Catalog, viewModel.CurrentViewModel);
        Assert.Equal(
            "Sign in with a support or admin account to access Orders.",
            viewModel.StatusText);
    }

    [Fact]
    public async Task ClearTroubleshootingContextRemovesOwnedPaymentsFilter()
    {
        var authentication = new AuthenticationState(
            new AuthenticatedUser(
                "support-123",
                "sam.support",
                "sam.support@example.com",
                ["support"]));
        ShellViewModel viewModel = CreateViewModel(authentication);
        Guid orderId = Guid.NewGuid();

        await viewModel.OpenPaymentsForOrderCommand.ExecuteAsync(orderId);

        Assert.Equal(orderId.ToString("D"), viewModel.Payments.SearchText);
        Assert.True(viewModel.HasTroubleshootingContext);

        viewModel.ClearTroubleshootingContextCommand.Execute(null);

        Assert.False(viewModel.HasTroubleshootingContext);
        Assert.Null(viewModel.ActiveTroubleshootingContext);
        Assert.Equal(string.Empty, viewModel.Payments.SearchText);
        Assert.Equal("Troubleshooting context cleared.", viewModel.StatusText);
    }

    [Fact]
    public async Task ClearTroubleshootingContextPreservesUserChangedSearch()
    {
        var authentication = new AuthenticationState(
            new AuthenticatedUser(
                "support-123",
                "sam.support",
                "sam.support@example.com",
                ["support"]));
        ShellViewModel viewModel = CreateViewModel(authentication);
        Guid orderId = Guid.NewGuid();

        await viewModel.OpenPaymentsForOrderCommand.ExecuteAsync(orderId);
        viewModel.Payments.SearchText = "Failed";

        viewModel.ClearTroubleshootingContextCommand.Execute(null);

        Assert.Equal("Failed", viewModel.Payments.SearchText);
        Assert.False(viewModel.HasTroubleshootingContext);
    }

    [Fact]
    public async Task ManualNavigationClearsTroubleshootingContext()
    {
        var authentication = new AuthenticationState(
            new AuthenticatedUser(
                "support-123",
                "sam.support",
                "sam.support@example.com",
                ["support"]));
        ShellViewModel viewModel = CreateViewModel(authentication);
        Guid orderId = Guid.NewGuid();

        await viewModel.OpenPaymentsForOrderCommand.ExecuteAsync(orderId);
        Assert.True(viewModel.HasTroubleshootingContext);

        viewModel.ShowCatalogCommand.Execute(null);

        Assert.Same(viewModel.Catalog, viewModel.CurrentViewModel);
        Assert.False(viewModel.HasTroubleshootingContext);
        Assert.Equal(string.Empty, viewModel.Payments.SearchText);
    }

    [Fact]
    public async Task ManualNavigationClearsInspectingStatus()
    {
        var authentication = new AuthenticationState(
            new AuthenticatedUser(
                "support-123",
                "sam.support",
                "sam.support@example.com",
                ["support"]));
        ShellViewModel viewModel = CreateViewModel(authentication);
        Guid orderId = Guid.NewGuid();

        await viewModel.OpenPaymentsForOrderCommand.ExecuteAsync(orderId);

        Assert.Contains(
            "Inspecting payments for order",
            viewModel.StatusText,
            StringComparison.Ordinal);

        viewModel.ShowInventoryCommand.Execute(null);

        Assert.Equal("Signed in as sam.support.", viewModel.StatusText);
    }

    [Fact]
    public async Task ClearTroubleshootingContextClearsDirectOrderDetail()
    {
        var authentication = new AuthenticationState(
            new AuthenticatedUser(
                "support-123",
                "sam.support",
                "sam.support@example.com",
                ["support"]));
        Guid orderId = Guid.NewGuid();
        OperationalOrderDetailDto detail = new(
            orderId,
            "customer-123",
            "customer@example.com",
            "Confirmed",
            1499.50m,
            "CZK",
            "test-success",
            DateTimeOffset.UtcNow,
            null,
            [],
            []);
        var ordersApiClient = new StubOrdersApiClient(
            (_, _) => Task.FromResult(detail));
        ShellViewModel viewModel = CreateViewModel(
            authentication,
            ordersApiClient);

        await viewModel.OpenOrderCommand.ExecuteAsync(orderId);

        Assert.Equal(orderId, viewModel.Orders.DetailOrderId);
        Assert.Same(detail, viewModel.Orders.SelectedOrderDetail);

        viewModel.ClearTroubleshootingContextCommand.Execute(null);

        Assert.Null(viewModel.Orders.DetailOrderId);
        Assert.Null(viewModel.Orders.SelectedOrderDetail);
        Assert.False(viewModel.HasTroubleshootingContext);
    }

    [Fact]
    public async Task OpenOrderCommandOpensExactOrderForSupportUser()
    {
        var authentication = new AuthenticationState(
            new AuthenticatedUser(
                "support-123",
                "sam.support",
                "sam.support@example.com",
                ["support"]));
        Guid orderId = Guid.NewGuid();
        OperationalOrderDetailDto detail = new(
            orderId,
            "customer-123",
            "customer@example.com",
            "Confirmed",
            1499.50m,
            "CZK",
            "test-success",
            DateTimeOffset.UtcNow,
            null,
            [],
            []);
        var ordersApiClient = new StubOrdersApiClient(
            (_, _) => Task.FromResult(detail));

        ShellViewModel viewModel = CreateViewModel(
            authentication,
            ordersApiClient);

        await viewModel.OpenOrderCommand.ExecuteAsync(orderId);

        Assert.Same(viewModel.Orders, viewModel.CurrentViewModel);
        Assert.Equal(orderId, viewModel.Orders.DetailOrderId);
        Assert.Same(detail, viewModel.Orders.SelectedOrderDetail);
        Assert.Equal($"Inspecting order {orderId:D}.", viewModel.StatusText);
        Assert.Equal(
            TroubleshootingContextKind.PaymentToOrder,
            viewModel.ActiveTroubleshootingContext?.Kind);
        Assert.Equal(
            orderId,
            viewModel.ActiveTroubleshootingContext?.CorrelationId);
        Assert.Equal(
            $"Payments → Order {orderId.ToString("D")[..8]}…",
            viewModel.TroubleshootingContextText);
    }

    private static ShellViewModel CreateViewModel(
        AuthenticationState? authentication = null,
        IOrdersApiClient? ordersApiClient = null)
    {
        IOptions<DesktopOptions> options =
            Options.Create(
                new DesktopOptions
                {
                    EnvironmentName = "Local"
                });

        var catalogViewModel =
            new CatalogViewModel(
                new StubCatalogApiClient(),
                NullLogger<CatalogViewModel>.Instance);

        authentication ??=
            new AuthenticationState();

        var inventoryViewModel =
            new InventoryViewModel(
                new StubInventoryApiClient(),
                authentication,
                new StubInventoryStockAdjustmentDialogService(),
                NullLogger<InventoryViewModel>.Instance);

        var paymentsViewModel =
            new PaymentsViewModel(
                new StubPaymentsApiClient(),
                NullLogger<PaymentsViewModel>.Instance);

        var ordersViewModel =
            new OrdersViewModel(
                ordersApiClient ?? new StubOrdersApiClient(),
                NullLogger<OrdersViewModel>.Instance);

        DiagnosticsViewModel diagnosticsViewModel =
            CreateDiagnosticsViewModel();

        var authenticationService =
            new StubAuthenticationService();

        return new ShellViewModel(
            options,
            catalogViewModel,
            inventoryViewModel,
            ordersViewModel,
            paymentsViewModel,
            diagnosticsViewModel,
            authenticationService,
            authentication);
    }

    private static DiagnosticsViewModel CreateDiagnosticsViewModel()
    {
        IOptions<DesktopOptions> desktopOptions =
            Options.Create(
                new DesktopOptions
                {
                    EnvironmentName = "Local"
                });

        IOptions<ApiGatewayOptions> apiGatewayOptions =
            Options.Create(
                new ApiGatewayOptions
                {
                    BaseAddress = "http://localhost:5080/",
                    TimeoutSeconds = 15
                });

        return new DiagnosticsViewModel(
            desktopOptions,
            apiGatewayOptions);
    }

    private sealed class StubCatalogApiClient
        : ICatalogApiClient
    {
        public Task<IReadOnlyList<CatalogProductDto>> GetProductsAsync(
            bool includeInactive,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyList<CatalogProductDto>>(
                []);
        }
    }

    private sealed class StubInventoryApiClient
        : IInventoryApiClient
    {
        public Task<IReadOnlyList<InventoryItemDto>>
            GetInventoryItemsAsync(
                bool includeInactive,
                CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyList<InventoryItemDto>>(
                []);
        }

        public Task<InventoryStockAdjustmentHistoryPageDto>
            GetStockAdjustmentHistoryAsync(
                Guid inventoryItemId,
                int offset,
                int limit,
                CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "Stock adjustment history was not expected in this test.");
        }

        public Task<InventoryStockAdjustmentResult>
            AdjustStockAsync(
                InventoryStockAdjustmentRequest request,
                CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "Stock adjustment was not expected in this test.");
        }
    }

    private sealed class StubPaymentsApiClient
        : IPaymentsApiClient
    {
        public Task<IReadOnlyList<PaymentDto>> GetPaymentsAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyList<PaymentDto>>(
                []);
        }
    }

    private sealed class StubAuthenticationService
        : IAuthenticationService
    {
        public Task<AuthenticationOperationResult> SignInAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                AuthenticationOperationResult.Success());
        }

        public Task<AuthenticationOperationResult> SignOutAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                AuthenticationOperationResult.Success());
        }
    }

    private sealed class StubOrdersApiClient(
        Func<Guid, CancellationToken, Task<OperationalOrderDetailDto>>? getOrder = null)
        : IOrdersApiClient
    {
        public Task<OperationalOrderPageDto> GetOrdersAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new OperationalOrderPageDto(
                    [],
                    offset,
                    limit,
                    false));
        }

        public Task<OperationalOrderDetailDto> GetOrderAsync(
            Guid orderId,
            CancellationToken cancellationToken)
        {
            if (getOrder is null)
            {
                throw new InvalidOperationException(
                    "Order detail was not expected in this test.");
            }

            return getOrder(orderId, cancellationToken);
        }
    }

    private sealed class StubInventoryStockAdjustmentDialogService
        : IInventoryStockAdjustmentDialogService
    {
        public InventoryStockAdjustmentDraft? ShowConfirmation(
            InventoryItemDto item)
        {
            return null;
        }
    }
}
