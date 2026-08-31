using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Orders;
using Eshop.Operations.Desktop.Models;

using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class OrdersViewModel : ObservableObject
{
    private const string AllStatusesLabel = "All statuses";
    private const int OrdersPageSize = 25;

    private readonly IOrdersApiClient _ordersApiClient;
    private readonly ILogger<OrdersViewModel> _logger;
    private CancellationTokenSource? _detailLoadCancellation;
    private int _nextOffset;

    public OrdersViewModel(
        IOrdersApiClient ordersApiClient,
        ILogger<OrdersViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(ordersApiClient);
        ArgumentNullException.ThrowIfNull(logger);

        _ordersApiClient = ordersApiClient;
        _logger = logger;

        OrdersView = CollectionViewSource.GetDefaultView(Orders);
        OrdersView.Filter = FilterOrder;
        SelectedSortOption = SortOptions[0];
        ApplySort(SelectedSortOption);
    }

    public ObservableCollection<OperationalOrderSummaryDto> Orders { get; } = [];
    public ICollectionView OrdersView { get; }

    public ObservableCollection<string> Statuses { get; } = [AllStatusesLabel];

    public IReadOnlyList<ListSortOption> SortOptions { get; } =
    [
        new("Created newest first", nameof(OperationalOrderSummaryDto.CreatedAtUtc), ListSortDirection.Descending),
        new("Created oldest first", nameof(OperationalOrderSummaryDto.CreatedAtUtc), ListSortDirection.Ascending),
        new("Total high to low", nameof(OperationalOrderSummaryDto.TotalAmount), ListSortDirection.Descending),
        new("Total low to high", nameof(OperationalOrderSummaryDto.TotalAmount), ListSortDirection.Ascending),
        new("Status A–Z", nameof(OperationalOrderSummaryDto.Status), ListSortDirection.Ascending),
        new("Customer A–Z", nameof(OperationalOrderSummaryDto.CustomerEmail), ListSortDirection.Ascending)
    ];

    public bool HasOrders => Orders.Count > 0;
    public bool HasVisibleOrders => !OrdersView.IsEmpty;
    public bool IsInitialState => !HasLoaded && !IsLoading && ErrorMessage is null;
    public bool IsEmpty => HasLoaded && !IsLoading && ErrorMessage is null && Orders.Count == 0;
    public bool IsFilteredEmpty => HasLoaded && !IsLoading && ErrorMessage is null && Orders.Count > 0 && !HasVisibleOrders;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanLoadMore => HasLoaded && HasMore && !IsLoading;
    public bool HasSelectedOrder => SelectedOrder is not null;
    public bool HasOrderDetail => SelectedOrderDetail is not null;
    public bool HasDetailError => !string.IsNullOrWhiteSpace(DetailErrorMessage);
    public string FilterScopeText => HasLoaded
        ? $"Filters apply to the {Orders.Count} currently loaded order(s)."
        : "Filters apply to loaded orders only.";

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedStatus { get; set; } = AllStatusesLabel;

    [ObservableProperty]
    public partial ListSortOption? SelectedSortOption { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedOrder))]
    public partial OperationalOrderSummaryDto? SelectedOrder { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOrderDetail))]
    public partial OperationalOrderDetailDto? SelectedOrderDetail { get; private set; }

    [ObservableProperty]
    public partial Guid? DetailOrderId { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInitialState))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsFilteredEmpty))]
    [NotifyPropertyChangedFor(nameof(CanLoadMore))]
    public partial bool HasLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInitialState))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsFilteredEmpty))]
    [NotifyPropertyChangedFor(nameof(CanLoadMore))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMore))]
    public partial bool HasMore { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsInitialState))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsFilteredEmpty))]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } = "Orders not loaded.";

    [ObservableProperty]
    public partial bool IsDetailLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetailError))]
    public partial string? DetailErrorMessage { get; private set; }

    [ObservableProperty]
    public partial string DetailStatusText { get; private set; } = "Select an order to view details.";

    partial void OnSearchTextChanged(string value) => RefreshOrdersView();
    partial void OnSelectedStatusChanged(string value) => RefreshOrdersView();

    partial void OnSelectedSortOptionChanged(ListSortOption? value)
    {
        if (value is not null)
        {
            ApplySort(value);
        }
    }

    partial void OnSelectedOrderChanged(OperationalOrderSummaryDto? value)
    {
        CancelDetailLoad();
        IsDetailLoading = false;
        SelectedOrderDetail = null;
        DetailOrderId = value?.Id;
        DetailErrorMessage = null;
        DetailStatusText = value is null
            ? "Select an order to view details."
            : $"Loading details for order {value.Id:D}...";
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LoadOrdersAsync(CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        StatusText = HasLoaded ? "Refreshing orders..." : "Loading orders...";

        try
        {
            OperationalOrderPageDto page = await _ordersApiClient.GetOrdersAsync(
                0,
                OrdersPageSize,
                cancellationToken);

            ReplaceOrders(page.Items);
            _nextOffset = page.Offset + page.Items.Count;
            HasMore = page.HasMore;
            HasLoaded = true;
            StatusText = BuildLoadedStatus();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Orders load canceled.";
        }
        catch (Exception exception)
        {
            HandleListFailure(exception);
        }
        finally
        {
            IsLoading = false;
            NotifyOrderCollectionStateChanged();
        }
    }

    [RelayCommand]
    private async Task LoadMoreOrdersAsync(CancellationToken cancellationToken)
    {
        if (!CanLoadMore)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        StatusText = "Loading more orders...";

        try
        {
            OperationalOrderPageDto page = await _ordersApiClient.GetOrdersAsync(
                _nextOffset,
                OrdersPageSize,
                cancellationToken);

            AppendOrders(page.Items);
            _nextOffset = page.Offset + page.Items.Count;
            HasMore = page.HasMore;
            StatusText = BuildLoadedStatus();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Orders load canceled.";
        }
        catch (Exception exception)
        {
            HandleListFailure(exception);
        }
        finally
        {
            IsLoading = false;
            NotifyOrderCollectionStateChanged();
        }
    }

    public async Task FocusOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Order id must not be empty.",
                nameof(orderId));
        }

        SelectedStatus = AllStatusesLabel;

        if (HasLoaded)
        {
            SearchText = orderId.ToString("D");
        }

        SelectedOrder = null;
        DetailOrderId = orderId;
        StatusText = HasLoaded
            ? $"Inspecting order {orderId:D}. The list filter applies to loaded orders only."
            : $"Inspecting order {orderId:D} directly. The order list is not loaded.";

        await LoadOrderDetailByIdAsync(orderId, cancellationToken);
    }

    public void ClearContextFocus(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            return;
        }

        string expectedSearchText = orderId.ToString("D");
        if (string.Equals(
                SearchText,
                expectedSearchText,
                StringComparison.OrdinalIgnoreCase))
        {
            SearchText = string.Empty;
        }

        if (DetailOrderId != orderId || SelectedOrder is not null)
        {
            return;
        }

        CancelDetailLoad();
        IsDetailLoading = false;
        SelectedOrderDetail = null;
        DetailOrderId = null;
        DetailErrorMessage = null;
        DetailStatusText = "Select an order to view details.";
        StatusText = HasLoaded
            ? BuildLoadedStatus()
            : "Orders not loaded.";
    }

    public Task LoadOrderDetailAsync(OperationalOrderSummaryDto? order)
    {
        if (order is null || SelectedOrder?.Id != order.Id)
        {
            return Task.CompletedTask;
        }

        return LoadOrderDetailByIdAsync(order.Id, CancellationToken.None);
    }

    private async Task LoadOrderDetailByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        CancelDetailLoad();
        SelectedOrderDetail = null;
        DetailOrderId = orderId;
        DetailErrorMessage = null;
        IsDetailLoading = true;
        DetailStatusText = $"Loading details for order {orderId:D}...";

        using CancellationTokenSource cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _detailLoadCancellation = cancellation;

        try
        {
            OperationalOrderDetailDto detail = await _ordersApiClient.GetOrderAsync(
                orderId,
                cancellation.Token);

            if (!ReferenceEquals(_detailLoadCancellation, cancellation)
                || DetailOrderId != orderId)
            {
                return;
            }

            SelectedOrderDetail = detail;
            DetailStatusText = $"Order {orderId:D} loaded.";
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            // A newer selection/context request owns the detail panel.
        }
        catch (Exception exception)
        {
            if (!ReferenceEquals(_detailLoadCancellation, cancellation)
                || DetailOrderId != orderId)
            {
                return;
            }

            DetailErrorMessage = GetLoadErrorMessage(
                exception,
                "The order detail request failed.");
            DetailStatusText = "Order detail load failed.";
            if (IsUnexpectedFailure(exception))
            {
                LogUnexpectedOrderDetailFailure(_logger, exception);
            }
        }
        finally
        {
            if (ReferenceEquals(_detailLoadCancellation, cancellation))
            {
                _detailLoadCancellation = null;
                IsDetailLoading = false;
            }
        }
    }

    [RelayCommand]
    private void ResetView()
    {
        SearchText = string.Empty;
        SelectedStatus = AllStatusesLabel;
        SelectedSortOption = SortOptions[0];
    }

    private bool FilterOrder(object item)
    {
        if (item is not OperationalOrderSummaryDto order)
        {
            return false;
        }

        if (!string.Equals(SelectedStatus, AllStatusesLabel, StringComparison.Ordinal)
            && !string.Equals(order.Status, SelectedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string searchText = SearchText.Trim();
        return searchText.Length == 0
            || order.CustomerEmail.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || order.CustomerId.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || order.Status.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || order.Id.ToString("D").Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshOrdersView()
    {
        OrdersView.Refresh();
        if (SelectedOrder is not null && !OrdersView.Contains(SelectedOrder))
        {
            SelectedOrder = null;
        }

        OnPropertyChanged(nameof(HasVisibleOrders));
        OnPropertyChanged(nameof(IsFilteredEmpty));
    }

    private void ApplySort(ListSortOption sortOption)
    {
        OrdersView.SortDescriptions.Clear();
        OrdersView.SortDescriptions.Add(new SortDescription(sortOption.PropertyName, sortOption.Direction));
    }

    private void RebuildStatuses()
    {
        string previousStatus = SelectedStatus;
        Statuses.Clear();
        Statuses.Add(AllStatusesLabel);

        foreach (string status in Orders.Select(order => order.Status)
                     .Where(status => !string.IsNullOrWhiteSpace(status))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(status => status, StringComparer.OrdinalIgnoreCase))
        {
            Statuses.Add(status);
        }

        SelectedStatus = Statuses.Any(status => string.Equals(status, previousStatus, StringComparison.OrdinalIgnoreCase))
            ? previousStatus
            : AllStatusesLabel;
    }

    private void ReplaceOrders(IReadOnlyList<OperationalOrderSummaryDto> orders)
    {
        Guid? selectedId = SelectedOrder?.Id;
        Orders.Clear();
        foreach (OperationalOrderSummaryDto order in orders)
        {
            Orders.Add(order);
        }

        RebuildStatuses();
        OrdersView.Refresh();
        OperationalOrderSummaryDto? refreshedSelection =
            selectedId is null
                ? null
                : Orders.FirstOrDefault(
                    order => order.Id == selectedId.Value);

        SelectedOrder = refreshedSelection is not null
            && OrdersView.Contains(refreshedSelection)
            ? refreshedSelection
            : null;
        NotifyOrderCollectionStateChanged();
    }

    private void AppendOrders(IReadOnlyList<OperationalOrderSummaryDto> orders)
    {
        HashSet<Guid> existingIds = Orders.Select(order => order.Id).ToHashSet();
        foreach (OperationalOrderSummaryDto order in orders)
        {
            if (existingIds.Add(order.Id))
            {
                Orders.Add(order);
            }
        }

        RebuildStatuses();
        OrdersView.Refresh();
        NotifyOrderCollectionStateChanged();
    }

    private void NotifyOrderCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasOrders));
        OnPropertyChanged(nameof(HasVisibleOrders));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsFilteredEmpty));
        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(FilterScopeText));
    }

    private string BuildLoadedStatus() => HasMore
        ? $"{Orders.Count} order(s) loaded. More orders are available."
        : $"{Orders.Count} order(s) loaded.";

    private void HandleListFailure(Exception exception)
    {
        ErrorMessage = GetLoadErrorMessage(exception, "The Orders request failed.");
        StatusText = "Orders load failed.";
        if (IsUnexpectedFailure(exception))
        {
            LogUnexpectedOrdersFailure(_logger, exception);
        }
    }

    private static string GetLoadErrorMessage(Exception exception, string fallback)
    {
        return exception switch
        {
            UnauthorizedAccessException => "Authentication is required to load Orders.",
            ApiRequestException { StatusCode: HttpStatusCode.Unauthorized } => "Your authentication session expired. Sign in again.",
            ApiRequestException { StatusCode: HttpStatusCode.Forbidden } => "Your account does not have permission to access Orders.",
            OperationCanceledException => "The Orders request timed out.",
            HttpRequestException => "The API Gateway could not be reached.",
            _ => fallback
        };
    }

    private static bool IsUnexpectedFailure(Exception exception) =>
        exception is not UnauthorizedAccessException
        && exception is not ApiRequestException
        && exception is not OperationCanceledException
        && exception is not HttpRequestException;

    private void CancelDetailLoad()
    {
        _detailLoadCancellation?.Cancel();
        _detailLoadCancellation = null;
    }

    [LoggerMessage(EventId = 5500, Level = LogLevel.Error, Message = "Unexpected Orders list failure.")]
    private static partial void LogUnexpectedOrdersFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5501, Level = LogLevel.Error, Message = "Unexpected Orders detail failure.")]
    private static partial void LogUnexpectedOrderDetailFailure(ILogger logger, Exception exception);
}
