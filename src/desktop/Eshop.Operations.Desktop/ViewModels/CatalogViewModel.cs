using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Catalog;
using Eshop.Operations.Desktop.Models;
using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class CatalogViewModel : ObservableObject
{
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
    }

    public ObservableCollection<CatalogProductDto> Products { get; } = [];

    public bool HasProducts => Products.Count > 0;

    public bool IsInitialState =>
        !HasLoaded
        && !IsLoading
        && Error is null;

    public bool IsEmpty =>
        HasLoaded
        && !IsLoading
        && Error is null
        && Products.Count == 0;

    public bool HasError => Error is not null;

    [ObservableProperty]
    public partial CatalogProductDto? SelectedProduct { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInitialState))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool HasLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInitialState))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsInitialState))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial CatalogLoadError? Error { get; private set; }

    [ObservableProperty]
    public partial string LoadStatus { get; private set; } =
        "Catalog not loaded.";

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

    private void ReplaceProducts(
        IReadOnlyList<CatalogProductDto> products)
    {
        SelectedProduct = null;

        Products.Clear();

        foreach (CatalogProductDto product in products)
        {
            Products.Add(product);
        }

        OnPropertyChanged(nameof(HasProducts));
        OnPropertyChanged(nameof(IsEmpty));
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
