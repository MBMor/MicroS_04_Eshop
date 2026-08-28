using Eshop.Operations.Desktop.Api.Catalog;
using Eshop.Operations.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.Globalization;
using System.Net;
using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Models;

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

    [Fact]
    public void SelectedProductIsInitiallyNull()
    {
        CatalogViewModel viewModel =
            CreateViewModel(
                new StubCatalogApiClient(
                    (_, _) =>
                        Task.FromResult<
                            IReadOnlyList<CatalogProductDto>>([])));

        Assert.Null(viewModel.SelectedProduct);
    }

    [Fact]
    public void SelectedProductRaisesPropertyChangedWhenChanged()
    {
        CatalogProductDto product = CreateProduct();

        CatalogViewModel viewModel =
            CreateViewModel(
                new StubCatalogApiClient(
                    (_, _) =>
                        Task.FromResult<
                            IReadOnlyList<CatalogProductDto>>([])));

        string? propertyName = null;

        viewModel.PropertyChanged += (_, args) =>
        {
            propertyName = args.PropertyName;
        };

        viewModel.SelectedProduct = product;

        Assert.Equal(
            nameof(CatalogViewModel.SelectedProduct),
            propertyName);
    }

    [Fact]
    public async Task LoadProductsCommandShowsEmptyStateForEmptyResponse()
    {
        var apiClient = new StubCatalogApiClient(
            (_, _) =>
                Task.FromResult<IReadOnlyList<CatalogProductDto>>([]));

        CatalogViewModel viewModel =
            CreateViewModel(apiClient);

        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasLoaded);
        Assert.True(viewModel.IsEmpty);
        Assert.False(viewModel.HasProducts);
        Assert.Null(viewModel.Error);
    }

    [Fact]
    public async Task LoadProductsCommandMapsConnectivityFailure()
    {
        var apiClient = new StubCatalogApiClient(
            (_, _) =>
                Task.FromException<IReadOnlyList<CatalogProductDto>>(
                    new HttpRequestException(
                        "Connection refused.")));

        CatalogViewModel viewModel =
            CreateViewModel(apiClient);

        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        Assert.NotNull(viewModel.Error);

        Assert.Equal(
            CatalogLoadErrorKind.Connectivity,
            viewModel.Error.Kind);

        Assert.False(viewModel.HasLoaded);
    }

    [Fact]
    public async Task LoadProductsCommandMapsTimeoutSeparatelyFromUserCancellation()
    {
        var apiClient = new StubCatalogApiClient(
            (_, _) =>
                Task.FromException<IReadOnlyList<CatalogProductDto>>(
                    new OperationCanceledException()));

        CatalogViewModel viewModel =
            CreateViewModel(apiClient);

        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        Assert.NotNull(viewModel.Error);

        Assert.Equal(
            CatalogLoadErrorKind.Timeout,
            viewModel.Error.Kind);
    }

    [Fact]
    public async Task LoadProductsCommandPreservesApiDiagnosticReference()
    {
        var problemDetails = new ApiProblemDetails
        {
            Status = 429,
            Title = "Too many requests",
            TraceId = "trace-123",
            RequestId = "request-456"
        };

        var apiClient = new StubCatalogApiClient(
            (_, _) =>
                Task.FromException<IReadOnlyList<CatalogProductDto>>(
                    new ApiRequestException(
                        HttpStatusCode.TooManyRequests,
                        problemDetails)));

        CatalogViewModel viewModel =
            CreateViewModel(apiClient);

        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        Assert.NotNull(viewModel.Error);

        Assert.Equal(
            CatalogLoadErrorKind.RateLimited,
            viewModel.Error.Kind);

        Assert.Equal(
            "trace-123",
            viewModel.Error.DiagnosticReference);
    }

    [Fact]
    public async Task FailedRefreshKeepsPreviouslyLoadedProducts()
    {
        CatalogProductDto product = CreateProduct();

        var callCount = 0;

        var apiClient = new StubCatalogApiClient(
            (_, _) =>
            {
                callCount++;

                if (callCount == 1)
                {
                    return Task.FromResult<
                        IReadOnlyList<CatalogProductDto>>(
                        [product]);
                }

                return Task.FromException<
                    IReadOnlyList<CatalogProductDto>>(
                    new HttpRequestException(
                        "Gateway unavailable."));
            });

        CatalogViewModel viewModel =
            CreateViewModel(apiClient);

        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        viewModel.SelectedProduct =
            Assert.Single(viewModel.Products);

        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        CatalogProductDto remainingProduct =
            Assert.Single(viewModel.Products);

        Assert.Same(
            product,
            remainingProduct);

        Assert.Same(
            product,
            viewModel.SelectedProduct);

        Assert.True(viewModel.HasLoaded);

        Assert.NotNull(viewModel.Error);

        Assert.Equal(
            CatalogLoadErrorKind.Connectivity,
            viewModel.Error.Kind);
    }

    [Fact]
    public async Task LoadProductsCommandIsDisabledWhileRequestIsRunning()
    {
        var apiClient =
            new CancellationObservingCatalogApiClient();

        CatalogViewModel viewModel =
            CreateViewModel(apiClient);

        Task requestTask =
            viewModel.LoadProductsCommand.ExecuteAsync(null);

        await apiClient.RequestStarted;

        Assert.False(
            viewModel.LoadProductsCommand.CanExecute(null));

        viewModel.LoadProductsCancelCommand.Execute(null);

        await requestTask;

        Assert.True(
            viewModel.LoadProductsCommand.CanExecute(null));
    }

    [Fact]
    public async Task SuccessfulRefreshPreservesSelectionByProductId()
    {
        Guid productId = Guid.NewGuid();

        CatalogProductDto original =
            CreateProduct() with
            {
                Id = productId,
                Name = "Original name"
            };

        CatalogProductDto refreshed =
            original with
            {
                Name = "Updated name"
            };

        var responses =
            new Queue<IReadOnlyList<CatalogProductDto>>(
            [
                [original],
            [refreshed]
            ]);

        var apiClient = new StubCatalogApiClient(
            (_, _) =>
                Task.FromResult(
                    responses.Dequeue()));

        CatalogViewModel viewModel =
            CreateViewModel(apiClient);

        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        viewModel.SelectedProduct =
            Assert.Single(viewModel.Products);

        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        Assert.NotNull(
            viewModel.SelectedProduct);

        Assert.Equal(
            productId,
            viewModel.SelectedProduct.Id);

        Assert.Equal(
            "Updated name",
            viewModel.SelectedProduct.Name);

        Assert.Same(
            refreshed,
            viewModel.SelectedProduct);
    }

    [Fact]
    public async Task SuccessfulRefreshClearsSelectionWhenProductWasRemoved()
    {
        CatalogProductDto product =
            CreateProduct();

        var responses =
            new Queue<IReadOnlyList<CatalogProductDto>>(
            [
                [product],
            []
            ]);

        var apiClient = new StubCatalogApiClient(
            (_, _) =>
                Task.FromResult(
                    responses.Dequeue()));

        CatalogViewModel viewModel =
            CreateViewModel(apiClient);

        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        viewModel.SelectedProduct =
            Assert.Single(viewModel.Products);

        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        Assert.Null(
            viewModel.SelectedProduct);

        Assert.Empty(
            viewModel.Products);
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
