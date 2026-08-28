using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Catalog;
using Eshop.Operations.Desktop.Models;
using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class CatalogViewModel : ObservableObject
{
    private const string AllCategoriesLabel = "All categories";

    private readonly ICatalogApiClient _catalogApiClient;
    private readonly ILogger<CatalogViewModel> _logger;

    public CatalogViewModel(
        ICatalogApiClient catalogApiClient,
        ILogger<CatalogViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(catalogApiClient);
        ArgumentNullException.ThrowIfNull(logger);

        _catalogApiClient = catalogApiClient;
        _logger = logger;

        ProductsView =
            CollectionViewSource.GetDefaultView(Products);

        ProductsView.Filter =
            FilterProduct;

        SelectedSortOption =
            SortOptions[0];
    }

    public ObservableCollection<CatalogProductDto> Products { get; } = [];

    public ICollectionView ProductsView { get; }

    public ObservableCollection<string> Categories { get; } =
        [AllCategoriesLabel];

    public IReadOnlyList<CatalogSortOption> SortOptions { get; } =
    [
        new(
            "Name A–Z",
            nameof(CatalogProductDto.Name),
            ListSortDirection.Ascending),

        new(
            "Name Z–A",
            nameof(CatalogProductDto.Name),
            ListSortDirection.Descending),

        new(
            "SKU A–Z",
            nameof(CatalogProductDto.Sku),
            ListSortDirection.Ascending),

        new(
            "Price low to high",
            nameof(CatalogProductDto.PriceAmount),
            ListSortDirection.Ascending),

        new(
            "Price high to low",
            nameof(CatalogProductDto.PriceAmount),
            ListSortDirection.Descending)
    ];

    public bool HasProducts =>
        Products.Count > 0;

    public bool HasVisibleProducts =>
        !ProductsView.IsEmpty;

    public bool IsInitialState =>
        !HasLoaded
        && !IsLoading
        && Error is null;

    public bool IsEmpty =>
        HasLoaded
        && !IsLoading
        && Error is null
        && Products.Count == 0;

    public bool IsFilteredEmpty =>
        HasLoaded
        && Products.Count > 0
        && !IsLoading
        && Error is null
        && ProductsView.IsEmpty;

    public bool HasError =>
        Error is not null;

    [ObservableProperty]
    public partial CatalogProductDto? SelectedProduct { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } =
        string.Empty;

    [ObservableProperty]
    public partial string SelectedCategory { get; set; } =
        AllCategoriesLabel;

    [ObservableProperty]
    public partial CatalogSortOption? SelectedSortOption { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInitialState))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsFilteredEmpty))]
    public partial bool HasLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInitialState))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsFilteredEmpty))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsInitialState))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsFilteredEmpty))]
    public partial CatalogLoadError? Error { get; private set; }

    [ObservableProperty]
    public partial string LoadStatus { get; private set; } =
        "Catalog not loaded.";

    partial void OnSearchTextChanged(
        string value)
    {
        RefreshProductsView();
    }

    partial void OnSelectedCategoryChanged(
        string value)
    {
        RefreshProductsView();
    }

    partial void OnSelectedSortOptionChanged(
        CatalogSortOption? value)
    {
        if (value is not null)
        {
            ApplySort(value);
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LoadProductsAsync(
        CancellationToken cancellationToken)
    {
        IsLoading = true;
        Error = null;

        LoadStatus = HasLoaded
            ? "Refreshing catalog..."
            : "Loading catalog...";

        try
        {
            IReadOnlyList<CatalogProductDto> products =
                await _catalogApiClient.GetProductsAsync(
                    includeInactive: false,
                    cancellationToken);

            ReplaceProducts(products);

            HasLoaded = true;

            LoadStatus =
                $"{Products.Count} product(s) loaded.";
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            LoadStatus = HasLoaded
                ? "Catalog refresh canceled."
                : "Catalog load canceled.";
        }
        catch (OperationCanceledException)
        {
            Error = new CatalogLoadError(
                CatalogLoadErrorKind.Timeout,
                "The Catalog request timed out.");

            LoadStatus = HasLoaded
                ? "Catalog refresh failed."
                : "Catalog load failed.";
        }
        catch (HttpRequestException)
        {
            Error = new CatalogLoadError(
                CatalogLoadErrorKind.Connectivity,
                "The API Gateway could not be reached.");

            LoadStatus = HasLoaded
                ? "Catalog refresh failed."
                : "Catalog load failed.";
        }
        catch (ApiRequestException exception)
        {
            Error = CreateApiError(exception);

            LoadStatus = HasLoaded
                ? "Catalog refresh failed."
                : "Catalog load failed.";
        }
        catch (Exception exception)
        {
            LogUnexpectedCatalogLoadFailure(
                _logger,
                exception);

            Error = new CatalogLoadError(
                CatalogLoadErrorKind.Unexpected,
                "An unexpected error occurred while loading the Catalog.");

            LoadStatus = HasLoaded
                ? "Catalog refresh failed."
                : "Catalog load failed.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ResetView()
    {
        SearchText =
            string.Empty;

        SelectedCategory =
            AllCategoriesLabel;

        SelectedSortOption =
            SortOptions[0];
    }

    private bool FilterProduct(
        object item)
    {
        if (item is not CatalogProductDto product)
        {
            return false;
        }

        if (!string.Equals(
                SelectedCategory,
                AllCategoriesLabel,
                StringComparison.Ordinal)
            && !string.Equals(
                product.Category,
                SelectedCategory,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string searchText =
            SearchText.Trim();

        if (searchText.Length == 0)
        {
            return true;
        }

        return product.Name.Contains(
                   searchText,
                   StringComparison.CurrentCultureIgnoreCase)
            || product.Sku.Contains(
                   searchText,
                   StringComparison.CurrentCultureIgnoreCase)
            || product.Category.Contains(
                   searchText,
                   StringComparison.CurrentCultureIgnoreCase);
    }

    private void RefreshProductsView()
    {
        ProductsView.Refresh();

        if (SelectedProduct is not null
            && !ProductsView.Contains(SelectedProduct))
        {
            SelectedProduct = null;
        }

        OnPropertyChanged(
            nameof(HasVisibleProducts));

        OnPropertyChanged(
            nameof(IsFilteredEmpty));
    }

    private void ApplySort(
        CatalogSortOption sortOption)
    {
        ProductsView.SortDescriptions.Clear();

        ProductsView.SortDescriptions.Add(
            new SortDescription(
                sortOption.PropertyName,
                sortOption.Direction));
    }

    private void RebuildCategories()
    {
        string previousCategory =
            SelectedCategory;

        Categories.Clear();

        Categories.Add(
            AllCategoriesLabel);

        IEnumerable<string> categories =
            Products
                .Select(
                    product => product.Category)
                .Where(
                    category =>
                        !string.IsNullOrWhiteSpace(category))
                .Distinct(
                    StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(
                    category => category,
                    StringComparer.CurrentCultureIgnoreCase);

        foreach (string category in categories)
        {
            Categories.Add(category);
        }

        bool categoryStillExists =
            Categories.Any(
                category =>
                    string.Equals(
                        category,
                        previousCategory,
                        StringComparison.OrdinalIgnoreCase));

        SelectedCategory =
            categoryStillExists
                ? previousCategory
                : AllCategoriesLabel;
    }

    private void ReplaceProducts(
        IReadOnlyList<CatalogProductDto> products)
    {
        Guid? selectedProductId =
            SelectedProduct?.Id;

        Products.Clear();

        foreach (CatalogProductDto product in products)
        {
            Products.Add(product);
        }

        RebuildCategories();

        ProductsView.Refresh();

        CatalogProductDto? refreshedSelection =
            selectedProductId is null
                ? null
                : Products.FirstOrDefault(
                    product =>
                        product.Id == selectedProductId.Value);

        SelectedProduct =
            refreshedSelection is not null
            && ProductsView.Contains(refreshedSelection)
                ? refreshedSelection
                : null;

        OnPropertyChanged(nameof(HasProducts));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasVisibleProducts));
        OnPropertyChanged(nameof(IsFilteredEmpty));
    }

    private static CatalogLoadError CreateApiError(
        ApiRequestException exception)
    {
        HttpStatusCode? statusCode = exception.StatusCode;
        CatalogLoadErrorKind kind;
        string message;

        switch (statusCode)
        {
            case HttpStatusCode.Unauthorized:
                kind = CatalogLoadErrorKind.Unauthorized;
                message =
                    "Authentication is required to load the Catalog.";
                break;

            case HttpStatusCode.Forbidden:
                kind = CatalogLoadErrorKind.Forbidden;
                message =
                    "You do not have permission to load the Catalog.";
                break;

            case HttpStatusCode.NotFound:
                kind = CatalogLoadErrorKind.NotFound;
                message =
                    "The Catalog endpoint was not found.";
                break;

            case HttpStatusCode.Conflict:
                kind = CatalogLoadErrorKind.Conflict;
                message =
                    "The Catalog request conflicted with the current server state.";
                break;

            case HttpStatusCode.TooManyRequests:
                kind = CatalogLoadErrorKind.RateLimited;
                message =
                    "Too many Catalog requests were made. Try again shortly.";
                break;

            case >= HttpStatusCode.InternalServerError:
                kind = CatalogLoadErrorKind.ServerFailure;
                message =
                    "The Catalog service is currently unavailable.";
                break;

            default:
                kind = CatalogLoadErrorKind.HttpFailure;
                message =
                    "The Catalog request failed.";
                break;
        }

        return new CatalogLoadError(
            kind,
            message,
            statusCode is null
                ? null
                : (int)statusCode.Value,
            exception.ProblemDetails?.TraceId,
            exception.ProblemDetails?.RequestId);
    }

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Error,
        Message =
            "An unexpected error occurred while loading the Catalog.")]
    private static partial void LogUnexpectedCatalogLoadFailure(
        ILogger logger,
        Exception exception);
}
