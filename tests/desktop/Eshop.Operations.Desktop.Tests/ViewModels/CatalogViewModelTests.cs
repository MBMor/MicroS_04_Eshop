using Eshop.Operations.Desktop.Api.Catalog;
using Eshop.Operations.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.Globalization;

namespace Eshop.Operations.Desktop.Tests.ViewModels;

public sealed class CatalogViewModelTests
{
    [Fact]
    public async Task LoadProductsCommandLoadsProducts()
    {
        CatalogProductDto product = CreateProduct();

        var apiClient = new StubCatalogApiClient(
            (_, _) =>
                Task.FromResult<IReadOnlyList<CatalogProductDto>>(
                    [product]));

        CatalogViewModel viewModel =
            CreateViewModel(apiClient);

        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        CatalogProductDto loadedProduct =
            Assert.Single(viewModel.Products);

        Assert.Equal(product, loadedProduct);

        Assert.Equal(
            "1 product(s) loaded.",
            viewModel.LoadStatus);
    }

    [Fact]
    public async Task LoadProductsCommandReplacesExistingProducts()
    {
        CatalogProductDto first = CreateProduct();

        CatalogProductDto second = first with
        {
            Id = Guid.NewGuid(),
            Name = "Replacement product",
            Sku = "REP-001"
        };

        var responses =
            new Queue<IReadOnlyList<CatalogProductDto>>(
            [
                [first],
                [second]
            ]);

        var apiClient = new StubCatalogApiClient(
            (_, _) =>
                Task.FromResult(responses.Dequeue()));

        CatalogViewModel viewModel =
            CreateViewModel(apiClient);

        await viewModel.LoadProductsCommand.ExecuteAsync(null);
        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        CatalogProductDto loadedProduct =
            Assert.Single(viewModel.Products);

        Assert.Equal(second, loadedProduct);
    }

    [Fact]
    public async Task LoadProductsCancelCommandCancelsPendingRequest()
    {
        var apiClient =
            new CancellationObservingCatalogApiClient();

        CatalogViewModel viewModel =
            CreateViewModel(apiClient);

        Task commandTask =
            viewModel.LoadProductsCommand.ExecuteAsync(null);

        await apiClient.RequestStarted;

        viewModel.LoadProductsCancelCommand.Execute(null);

        await commandTask;

        Assert.True(apiClient.CancellationObserved);

        Assert.Equal(
            "Catalog load canceled.",
            viewModel.LoadStatus);
    }

    [Fact]
    public async Task LoadProductsCommandHandlesFailure()
    {
        var apiClient = new StubCatalogApiClient(
            (_, _) =>
                Task.FromException<IReadOnlyList<CatalogProductDto>>(
                    new HttpRequestException(
                        "Gateway unavailable.")));

        CatalogViewModel viewModel =
            CreateViewModel(apiClient);

        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Products);

        Assert.Equal(
            "Catalog load failed.",
            viewModel.LoadStatus);
    }

    private static CatalogViewModel CreateViewModel(
        ICatalogApiClient apiClient)
    {
        return new CatalogViewModel(
            apiClient,
            NullLogger<CatalogViewModel>.Instance);
    }

    private static CatalogProductDto CreateProduct()
    {
        return new CatalogProductDto(
            Guid.NewGuid(),
            "Mechanical Keyboard",
            "KEY-001",
            "Mechanical keyboard",
            "Peripherals",
            129.90m,
            "EUR",
            true,
            DateTimeOffset.Parse(
                "2026-08-01T10:00:00+00:00",
                CultureInfo.InvariantCulture),
            null);
    }

    private sealed class StubCatalogApiClient(
        Func<
            bool,
            CancellationToken,
            Task<IReadOnlyList<CatalogProductDto>>> getProducts)
        : ICatalogApiClient
    {
        public Task<IReadOnlyList<CatalogProductDto>> GetProductsAsync(
            bool includeInactive,
            CancellationToken cancellationToken)
        {
            return getProducts(
                includeInactive,
                cancellationToken);
        }
    }

    private sealed class CancellationObservingCatalogApiClient
        : ICatalogApiClient
    {
        private readonly TaskCompletionSource _requestStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RequestStarted => _requestStarted.Task;

        public bool CancellationObserved { get; private set; }

        public async Task<IReadOnlyList<CatalogProductDto>>
            GetProductsAsync(
                bool includeInactive,
                CancellationToken cancellationToken)
        {
            _requestStarted.SetResult();

            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved = true;
                throw;
            }

            return [];
        }
    }
}
