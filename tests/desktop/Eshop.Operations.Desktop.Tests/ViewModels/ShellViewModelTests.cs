using Eshop.Operations.Desktop.Configuration;
using Eshop.Operations.Desktop.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;
using Eshop.Operations.Desktop.Api.Catalog;
using Microsoft.Extensions.Logging.Abstractions;

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

    private static ShellViewModel CreateViewModel()
    {
        var options = Options.Create(
            new DesktopOptions
            {
                EnvironmentName = "Local"
            });

        var catalogViewModel = new CatalogViewModel(
            new StubCatalogApiClient(),
            NullLogger<CatalogViewModel>.Instance);

        return new ShellViewModel(
            options,
            catalogViewModel);
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
