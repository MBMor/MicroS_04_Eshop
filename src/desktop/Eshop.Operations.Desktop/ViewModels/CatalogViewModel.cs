using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eshop.Operations.Desktop.Api.Catalog;
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

    [ObservableProperty]
    public partial string LoadStatus { get; private set; } =
        "Catalog not loaded.";

    [ObservableProperty]
    public partial CatalogProductDto? SelectedProduct { get; set; }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LoadProductsAsync(
        CancellationToken cancellationToken)
    {
        LoadStatus = "Loading catalog...";

        try
        {
            IReadOnlyList<CatalogProductDto> products =
                await _catalogApiClient.GetProductsAsync(
                    includeInactive: false,
                    cancellationToken);

            Products.Clear();

            foreach (CatalogProductDto product in products)
            {
                Products.Add(product);
            }

            LoadStatus =
                $"{Products.Count} product(s) loaded.";
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            LoadStatus = "Catalog load canceled.";
        }
        catch (Exception exception)
        {
            LogCatalogLoadFailed(
                _logger,
                exception);

            LoadStatus = "Catalog load failed.";
        }
    }

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Error,
        Message = "Catalog load failed.")]
    private static partial void LogCatalogLoadFailed(
        ILogger logger,
        Exception exception);
}
