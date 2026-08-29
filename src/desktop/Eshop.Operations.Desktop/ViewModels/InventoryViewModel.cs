using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Inventory;
using Eshop.Operations.Desktop.Models;
using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class InventoryViewModel
    : ObservableObject
{
    private readonly IInventoryApiClient _inventoryApiClient;
    private readonly ILogger<InventoryViewModel> _logger;

    public InventoryViewModel(
        IInventoryApiClient inventoryApiClient,
        ILogger<InventoryViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(
            inventoryApiClient);

        ArgumentNullException.ThrowIfNull(
            logger);

        _inventoryApiClient =
            inventoryApiClient;

        _logger =
            logger;

        ItemsView =
            CollectionViewSource.GetDefaultView(
                Items);

        ItemsView.Filter =
            FilterItem;

        SelectedSortOption =
            SortOptions[0];
    }

    public ObservableCollection<InventoryItemDto> Items { get; } = [];

    public ICollectionView ItemsView { get; }

    public IReadOnlyList<ListSortOption> SortOptions { get; } =
    [
        new(
            "SKU A–Z",
            nameof(InventoryItemDto.Sku),
            ListSortDirection.Ascending),

        new(
            "Available low to high",
            nameof(InventoryItemDto.AvailableQuantity),
            ListSortDirection.Ascending),

        new(
            "Available high to low",
            nameof(InventoryItemDto.AvailableQuantity),
            ListSortDirection.Descending),

        new(
            "On hand high to low",
            nameof(InventoryItemDto.OnHandQuantity),
            ListSortDirection.Descending)
    ];

    public bool HasItems =>
        Items.Count > 0;

    public bool HasVisibleItems =>
        !ItemsView.IsEmpty;

    public bool IsInitialState =>
        !HasLoaded
        && !IsLoading
        && ErrorMessage is null;

    public bool IsEmpty =>
        HasLoaded
        && !IsLoading
        && ErrorMessage is null
        && Items.Count == 0;

    public bool IsFilteredEmpty =>
        HasLoaded
        && Items.Count > 0
        && !IsLoading
        && ErrorMessage is null
        && ItemsView.IsEmpty;

    public bool HasError =>
        !string.IsNullOrWhiteSpace(
            ErrorMessage);

    [ObservableProperty]
    public partial InventoryItemDto? SelectedItem
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string SearchText
    {
        get;
        set;
    } = string.Empty;

    [ObservableProperty]
    public partial ListSortOption? SelectedSortOption
    {
        get;
        set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInitialState))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsFilteredEmpty))]
    public partial bool HasLoaded
    {
        get;
        private set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInitialState))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsFilteredEmpty))]
    public partial bool IsLoading
    {
        get;
        private set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsInitialState))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsFilteredEmpty))]
    public partial string? ErrorMessage
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string StatusText
    {
        get;
        private set;
    } = "Inventory not loaded.";

    partial void OnSearchTextChanged(
        string value)
    {
        RefreshItemsView();
    }

    partial void OnSelectedSortOptionChanged(
        ListSortOption? value)
    {
        if (value is not null)
        {
            ApplySort(value);
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LoadInventoryAsync(
        CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        StatusText =
            HasLoaded
                ? "Refreshing inventory..."
                : "Loading inventory...";

        try
        {
            IReadOnlyList<InventoryItemDto> items =
                await _inventoryApiClient
                    .GetInventoryItemsAsync(
                        includeInactive: false,
                        cancellationToken);

            ReplaceItems(
                items);

            HasLoaded = true;

            StatusText =
                $"{Items.Count} inventory item(s) loaded.";
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            StatusText =
                HasLoaded
                    ? "Inventory refresh canceled."
                    : "Inventory load canceled.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage =
                "Authentication is required to load Inventory.";

            StatusText =
                "Inventory load failed.";
        }
        catch (ApiRequestException exception)
            when (exception.StatusCode
                  == HttpStatusCode.Unauthorized)
        {
            ErrorMessage =
                "Your authentication session expired. Sign in again.";

            StatusText =
                "Inventory load failed.";
        }
        catch (ApiRequestException exception)
            when (exception.StatusCode
                  == HttpStatusCode.Forbidden)
        {
            ErrorMessage =
                "Your account does not have permission to access Inventory.";

            StatusText =
                "Inventory load failed.";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage =
                "The Inventory request timed out.";

            StatusText =
                "Inventory load failed.";
        }
        catch (HttpRequestException)
        {
            ErrorMessage =
                "The API Gateway could not be reached.";

            StatusText =
                "Inventory load failed.";
        }
        catch (Exception exception)
        {
            LogUnexpectedInventoryFailure(
                _logger,
                exception);

            ErrorMessage =
                "An unexpected error occurred while loading Inventory.";

            StatusText =
                "Inventory load failed.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ResetView()
    {
        SearchText = string.Empty;
        SelectedSortOption = SortOptions[0];
    }

    private bool FilterItem(object item)
    {
        if (item is not InventoryItemDto inventoryItem)
        {
            return false;
        }

        string searchText = SearchText.Trim();
        if (searchText.Length == 0)
        {
            return true;
        }

        return inventoryItem.Sku.Contains(
                   searchText,
                   StringComparison.CurrentCultureIgnoreCase)
               || inventoryItem.ProductId
                   .ToString()
                   .Contains(
                       searchText,
                       StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshItemsView()
    {
        ItemsView.Refresh();

        if (SelectedItem is not null
            && !ItemsView.Contains(SelectedItem))
        {
            SelectedItem = null;
        }

        OnPropertyChanged(nameof(HasVisibleItems));
        OnPropertyChanged(nameof(IsFilteredEmpty));
    }

    private void ApplySort(ListSortOption sortOption)
    {
        ItemsView.SortDescriptions.Clear();
        ItemsView.SortDescriptions.Add(
            new SortDescription(
                sortOption.PropertyName,
                sortOption.Direction));
    }

    private void ReplaceItems(
        IReadOnlyList<InventoryItemDto> items)
    {
        Guid? selectedId =
            SelectedItem?.Id;

        Items.Clear();

        foreach (InventoryItemDto item in items)
        {
            Items.Add(item);
        }

        ItemsView.Refresh();

        InventoryItemDto? refreshedSelection =
            selectedId is null
                ? null
                : Items.FirstOrDefault(
                    item => item.Id == selectedId.Value);

        SelectedItem =
            refreshedSelection is not null
            && ItemsView.Contains(refreshedSelection)
                ? refreshedSelection
                : null;

        OnPropertyChanged(
            nameof(HasItems));

        OnPropertyChanged(
            nameof(HasVisibleItems));

        OnPropertyChanged(
            nameof(IsEmpty));

        OnPropertyChanged(
            nameof(IsFilteredEmpty));
    }

    [LoggerMessage(
        EventId = 5100,
        Level = LogLevel.Error,
        Message =
            "An unexpected error occurred while loading Inventory.")]
    private static partial void LogUnexpectedInventoryFailure(
        ILogger logger,
        Exception exception);
}
