using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Catalog;
using Eshop.Operations.Desktop.Configuration;
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

        Assert.Equal(0, notificationCount);
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

    private static ShellViewModel CreateViewModel()
    {
        IOptions<DesktopOptions> options =
            Options.Create(
                new DesktopOptions
                {
                    EnvironmentName = "Local"
                });

        var catalogViewModel = new CatalogViewModel(
            new StubCatalogApiClient(),
            NullLogger<CatalogViewModel>.Instance);

        DiagnosticsViewModel diagnosticsViewModel =
            CreateDiagnosticsViewModel();

        return new ShellViewModel(
            options,
            catalogViewModel,
            diagnosticsViewModel);
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

    private sealed class StubCatalogApiClient : ICatalogApiClient
    {
        public Task<IReadOnlyList<CatalogProductDto>> GetProductsAsync(
            bool includeInactive,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<CatalogProductDto>>([]);
        }
    }
}
