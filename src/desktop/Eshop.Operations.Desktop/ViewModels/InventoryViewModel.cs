using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Inventory;
using Eshop.Operations.Desktop.Authentication;
using Eshop.Operations.Desktop.Models;
using Eshop.Operations.Desktop.Services;

using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class InventoryViewModel : ObservableObject
{
    private readonly IInventoryApiClient _inventoryApiClient;
    private readonly IInventoryStockAdjustmentDialogService
        _stockAdjustmentDialogService;
    private readonly ILogger<InventoryViewModel> _logger;

    private InventoryStockAdjustmentRequest? _unknownOutcomeRequest;

    public InventoryViewModel(
        IInventoryApiClient inventoryApiClient,
        AuthenticationState authentication,
        IInventoryStockAdjustmentDialogService stockAdjustmentDialogService,
        ILogger<InventoryViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(inventoryApiClient);
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(stockAdjustmentDialogService);
        ArgumentNullException.ThrowIfNull(logger);

        _inventoryApiClient = inventoryApiClient;
        Authentication = authentication;
        _stockAdjustmentDialogService = stockAdjustmentDialogService;
        _logger = logger;

        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = FilterItem;
        SelectedSortOption = SortOptions[0];
        Authentication.PropertyChanged += OnAuthenticationPropertyChanged;
    }

    public ObservableCollection<InventoryItemDto> Items { get; } = [];
    public AuthenticationState Authentication { get; }
    public ICollectionView ItemsView { get; }

    public IReadOnlyList<ListSortOption> SortOptions { get; } =
    [
        new("SKU A–Z", nameof(InventoryItemDto.Sku), ListSortDirection.Ascending),
        new("Available low to high", nameof(InventoryItemDto.AvailableQuantity), ListSortDirection.Ascending),
        new("Available high to low", nameof(InventoryItemDto.AvailableQuantity), ListSortDirection.Descending),
        new("On hand high to low", nameof(InventoryItemDto.OnHandQuantity), ListSortDirection.Descending)
    ];

    public bool HasItems => Items.Count > 0;
    public bool HasVisibleItems => !ItemsView.IsEmpty;
    public bool IsInitialState => !HasLoaded && !IsLoading && ErrorMessage is null;
    public bool IsEmpty => HasLoaded && !IsLoading && ErrorMessage is null && Items.Count == 0;
    public bool IsFilteredEmpty => HasLoaded && Items.Count > 0 && !IsLoading && ErrorMessage is null && ItemsView.IsEmpty;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanStartStockAdjustment => Authentication.CanAdjustInventory && SelectedItem is not null && !IsAdjustmentInProgress && _unknownOutcomeRequest is null;
    public bool HasAdjustmentError => !string.IsNullOrWhiteSpace(AdjustmentErrorMessage);
    public bool HasUnknownAdjustmentOutcome => _unknownOutcomeRequest is not null;
    public bool CanRetryUnknownStockAdjustment => Authentication.CanAdjustInventory && _unknownOutcomeRequest is not null && !IsAdjustmentInProgress;
    public string? UnknownOutcomeOperationId => _unknownOutcomeRequest?.IdempotencyKey.ToString("D");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartStockAdjustment))]
    public partial InventoryItemDto? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ListSortOption? SelectedSortOption { get; set; }

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
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } = "Inventory not loaded.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartStockAdjustment))]
    [NotifyPropertyChangedFor(nameof(CanRetryUnknownStockAdjustment))]
    public partial bool IsAdjustmentInProgress { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAdjustmentError))]
    public partial string? AdjustmentErrorMessage { get; private set; }

    [ObservableProperty]
    public partial string? UnknownOutcomeMessage { get; private set; }

    partial void OnSearchTextChanged(string value) => RefreshItemsView();

    partial void OnSelectedSortOptionChanged(ListSortOption? value)
    {
        if (value is not null) ApplySort(value);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LoadInventoryAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        StatusText = HasLoaded ? "Refreshing inventory..." : "Loading inventory...";
        try
        {
            IReadOnlyList<InventoryItemDto> items = await _inventoryApiClient.GetInventoryItemsAsync(includeInactive: false, cancellationToken);
            ReplaceItems(items);
            HasLoaded = true;
            StatusText = $"{Items.Count} inventory item(s) loaded.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = HasLoaded ? "Inventory refresh canceled." : "Inventory load canceled.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "Authentication is required to load Inventory.";
            StatusText = "Inventory load failed.";
        }
        catch (ApiRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            ErrorMessage = "Your authentication session expired. Sign in again.";
            StatusText = "Inventory load failed.";
        }
        catch (ApiRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            ErrorMessage = "Your account does not have permission to access Inventory.";
            StatusText = "Inventory load failed.";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "The Inventory request timed out.";
            StatusText = "Inventory load failed.";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "The API Gateway could not be reached.";
            StatusText = "Inventory load failed.";
        }
        catch (Exception exception)
        {
            LogUnexpectedInventoryFailure(_logger, exception);
            ErrorMessage = "An unexpected error occurred while loading Inventory.";
            StatusText = "Inventory load failed.";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task AdjustSelectedStockAsync(CancellationToken cancellationToken)
    {
        AdjustmentErrorMessage = null;
        if (!Authentication.CanAdjustInventory)
        {
            AdjustmentErrorMessage = "Only an administrator can adjust inventory stock.";
            return;
        }
        if (SelectedItem is null)
        {
            AdjustmentErrorMessage = "Select an inventory item before adjusting stock.";
            return;
        }
        if (_unknownOutcomeRequest is not null)
        {
            AdjustmentErrorMessage = "Resolve the previous unknown stock adjustment outcome before creating another adjustment.";
            return;
        }

        InventoryItemDto selectedItem = SelectedItem;
        InventoryStockAdjustmentDraft? draft = _stockAdjustmentDialogService.ShowConfirmation(selectedItem);
        if (draft is null)
        {
            StatusText = "Stock adjustment canceled.";
            return;
        }

        var request = new InventoryStockAdjustmentRequest(
            selectedItem.Id, draft.QuantityDelta, selectedItem.Version, draft.Reason, Guid.NewGuid());
        await ExecuteStockAdjustmentAsync(request, isRetry: false, cancellationToken);
    }

    [RelayCommand]
    private async Task RetryUnknownStockAdjustmentAsync(CancellationToken cancellationToken)
    {
        AdjustmentErrorMessage = null;
        if (!Authentication.CanAdjustInventory)
        {
            AdjustmentErrorMessage = "Sign in with an administrator account before retrying the unresolved stock adjustment.";
            return;
        }
        InventoryStockAdjustmentRequest? request = _unknownOutcomeRequest;
        if (request is null) return;
        await ExecuteStockAdjustmentAsync(request, isRetry: true, cancellationToken);
    }

    private async Task ExecuteStockAdjustmentAsync(InventoryStockAdjustmentRequest request, bool isRetry, CancellationToken cancellationToken)
    {
        IsAdjustmentInProgress = true;
        AdjustmentErrorMessage = null;
        StatusText = isRetry ? "Retrying unresolved stock adjustment..." : "Applying stock adjustment...";
        try
        {
            InventoryStockAdjustmentResult result = await _inventoryApiClient.AdjustStockAsync(request, cancellationToken);
            ApplyAdjustedItem(result.Item);
            ClearUnknownOutcome();
            StatusText = result.IsReplay ? "Stock adjustment confirmed by idempotent replay." : "Stock adjustment applied.";
        }
        catch (InventoryStockAdjustmentOutcomeUnknownException exception)
        {
            SetUnknownOutcome(request, "The stock adjustment result is unknown. The operation may already have been applied. Do not create a replacement adjustment. Retry this same operation.");
            StatusText = "Stock adjustment outcome unknown.";
            LogUnknownStockAdjustmentOutcome(_logger, exception);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (isRetry) AdjustmentErrorMessage = "The retry was canceled before a reliable result was obtained. The original operation remains unresolved.";
            else StatusText = "Stock adjustment canceled before it was sent.";
        }
        catch (UnauthorizedAccessException)
        {
            AdjustmentErrorMessage = isRetry ? "Authentication is required. The unresolved operation remains available for safe retry." : "Authentication is required to adjust inventory.";
        }
        catch (ApiRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            AdjustmentErrorMessage = isRetry ? "Your authentication session expired. The unresolved operation remains available for safe retry after signing in." : "Your authentication session expired. Sign in again.";
        }
        catch (ApiRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            AdjustmentErrorMessage = isRetry ? "The current account cannot retry this unresolved operation. Administrator access is required." : "Your account does not have permission to adjust inventory.";
        }
        catch (ApiRequestException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            AdjustmentErrorMessage = isRetry ? "The unresolved operation could not yet be replayed. Do not create a new adjustment; retry the same operation later." : GetApiFailureMessage(exception, "Inventory changed since it was loaded. Refresh Inventory and review the adjustment again.");
        }
        catch (ApiRequestException exception) when (exception.StatusCode == HttpStatusCode.BadRequest)
        {
            if (isRetry) ClearUnknownOutcome();
            AdjustmentErrorMessage = GetApiFailureMessage(exception, "The stock adjustment was rejected by server validation.");
        }
        catch (ApiRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            if (isRetry) ClearUnknownOutcome();
            AdjustmentErrorMessage = GetApiFailureMessage(exception, "The inventory item no longer exists. Refresh Inventory.");
        }
        catch (Exception exception)
        {
            SetUnknownOutcome(request, "An unexpected client failure occurred after the stock adjustment started. To avoid a duplicate mutation, do not create another adjustment. Retry this same operation.");
            StatusText = "Stock adjustment could not be confirmed.";
            LogUnexpectedStockAdjustmentFailure(_logger, exception);
        }
        finally { IsAdjustmentInProgress = false; }
    }

    private static string GetApiFailureMessage(ApiRequestException exception, string fallbackMessage)
    {
        string? detail = exception.ProblemDetails?.Detail;
        return string.IsNullOrWhiteSpace(detail) ? fallbackMessage : detail;
    }

    private void ApplyAdjustedItem(InventoryItemDto adjustedItem)
    {
        int index = -1;
        for (int itemIndex = 0; itemIndex < Items.Count; itemIndex++)
        {
            if (Items[itemIndex].Id == adjustedItem.Id) { index = itemIndex; break; }
        }
        if (index >= 0) Items[index] = adjustedItem; else Items.Add(adjustedItem);
        ItemsView.Refresh();
        SelectedItem = ItemsView.Contains(adjustedItem) ? adjustedItem : null;
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasVisibleItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsFilteredEmpty));
    }

    private void SetUnknownOutcome(InventoryStockAdjustmentRequest request, string message)
    {
        _unknownOutcomeRequest = request;
        UnknownOutcomeMessage = message;
        NotifyUnknownOutcomeStateChanged();
    }

    private void ClearUnknownOutcome()
    {
        _unknownOutcomeRequest = null;
        UnknownOutcomeMessage = null;
        NotifyUnknownOutcomeStateChanged();
    }

    private void NotifyUnknownOutcomeStateChanged()
    {
        OnPropertyChanged(nameof(HasUnknownAdjustmentOutcome));
        OnPropertyChanged(nameof(UnknownOutcomeOperationId));
        OnPropertyChanged(nameof(CanStartStockAdjustment));
        OnPropertyChanged(nameof(CanRetryUnknownStockAdjustment));
    }

    private void OnAuthenticationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AuthenticationState.CanAdjustInventory)) return;
        OnPropertyChanged(nameof(CanStartStockAdjustment));
        OnPropertyChanged(nameof(CanRetryUnknownStockAdjustment));
    }

    [RelayCommand]
    private void ResetView()
    {
        SearchText = string.Empty;
        SelectedSortOption = SortOptions[0];
    }

    private bool FilterItem(object item)
    {
        if (item is not InventoryItemDto inventoryItem) return false;
        string searchText = SearchText.Trim();
        if (searchText.Length == 0) return true;
        return inventoryItem.Sku.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)
            || inventoryItem.ProductId.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshItemsView()
    {
        ItemsView.Refresh();
        if (SelectedItem is not null && !ItemsView.Contains(SelectedItem)) SelectedItem = null;
        OnPropertyChanged(nameof(HasVisibleItems));
        OnPropertyChanged(nameof(IsFilteredEmpty));
    }

    private void ApplySort(ListSortOption sortOption)
    {
        ItemsView.SortDescriptions.Clear();
        ItemsView.SortDescriptions.Add(new SortDescription(sortOption.PropertyName, sortOption.Direction));
    }

    private void ReplaceItems(IReadOnlyList<InventoryItemDto> items)
    {
        Guid? selectedId = SelectedItem?.Id;
        Items.Clear();
        foreach (InventoryItemDto item in items) Items.Add(item);
        ItemsView.Refresh();
        InventoryItemDto? refreshedSelection = selectedId is null ? null : Items.FirstOrDefault(item => item.Id == selectedId.Value);
        SelectedItem = refreshedSelection is not null && ItemsView.Contains(refreshedSelection) ? refreshedSelection : null;
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasVisibleItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsFilteredEmpty));
    }

    [LoggerMessage(EventId = 5100, Level = LogLevel.Error, Message = "An unexpected error occurred while loading Inventory.")]
    private static partial void LogUnexpectedInventoryFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5101, Level = LogLevel.Warning, Message = "Inventory stock adjustment has an unknown outcome.")]
    private static partial void LogUnknownStockAdjustmentOutcome(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5102, Level = LogLevel.Error, Message = "An unexpected client error occurred while processing an inventory stock adjustment.")]
    private static partial void LogUnexpectedStockAdjustmentFailure(ILogger logger, Exception exception);
}
