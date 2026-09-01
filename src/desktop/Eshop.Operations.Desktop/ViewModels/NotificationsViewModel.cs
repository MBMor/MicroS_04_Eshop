using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Notifications;

using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class NotificationsViewModel
    : ObservableObject
{
    private const int PageSize =
        25;

    private readonly INotificationsApiClient
        _notificationsApiClient;

    private readonly ILogger<NotificationsViewModel>
        _logger;

    private int _nextOffset;

    public NotificationsViewModel(
        INotificationsApiClient notificationsApiClient,
        ILogger<NotificationsViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(
            notificationsApiClient);

        ArgumentNullException.ThrowIfNull(
            logger);

        _notificationsApiClient =
            notificationsApiClient;

        _logger =
            logger;
    }

    public ObservableCollection<
        OperationalNotificationDto>
        Notifications
    {
        get;
    } = [];

    public bool HasNotifications =>
        Notifications.Count > 0;

    public bool IsInitialState =>
        !HasLoaded
        && !IsLoading
        && ErrorMessage is null;

    public bool IsEmpty =>
        HasLoaded
        && !IsLoading
        && ErrorMessage is null
        && Notifications.Count == 0;

    public bool HasError =>
        !string.IsNullOrWhiteSpace(
            ErrorMessage);

    public bool HasSelectedNotification =>
        SelectedNotification is not null;

    public bool CanLoadMore =>
        HasLoaded
        && HasMore
        && !IsLoading;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(OrderIdText)
        || !string.IsNullOrWhiteSpace(CustomerIdText)
        || !string.IsNullOrWhiteSpace(CorrelationIdText);

    [ObservableProperty]
    public partial string OrderIdText
    {
        get;
        set;
    } = string.Empty;

    [ObservableProperty]
    public partial string CustomerIdText
    {
        get;
        set;
    } = string.Empty;

    [ObservableProperty]
    public partial string CorrelationIdText
    {
        get;
        set;
    } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasSelectedNotification))]
    public partial OperationalNotificationDto?
        SelectedNotification
    {
        get;
        set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsInitialState))]
    [NotifyPropertyChangedFor(
        nameof(IsEmpty))]
    [NotifyPropertyChangedFor(
        nameof(CanLoadMore))]
    public partial bool HasLoaded
    {
        get;
        private set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsInitialState))]
    [NotifyPropertyChangedFor(
        nameof(IsEmpty))]
    [NotifyPropertyChangedFor(
        nameof(CanLoadMore))]
    public partial bool IsLoading
    {
        get;
        private set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(CanLoadMore))]
    public partial bool HasMore
    {
        get;
        private set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasError))]
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
    } =
        "Notifications not loaded.";

    partial void OnOrderIdTextChanged(
        string value)
    {
        OnPropertyChanged(
            nameof(HasActiveFilters));
    }

    partial void OnCustomerIdTextChanged(
        string value)
    {
        OnPropertyChanged(
            nameof(HasActiveFilters));
    }

    partial void OnCorrelationIdTextChanged(
        string value)
    {
        OnPropertyChanged(
            nameof(HasActiveFilters));
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LoadNotificationsAsync(
        CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return;
        }

        if (!TryGetFilters(
                out Guid? orderId,
                out string? customerId,
                out Guid? correlationId))
        {
            return;
        }

        IsLoading =
            true;

        ErrorMessage =
            null;

        StatusText =
            HasLoaded
                ? "Refreshing notifications..."
                : "Loading notifications...";

        try
        {
            OperationalNotificationPageDto page =
                await _notificationsApiClient
                    .GetNotificationsAsync(
                        orderId,
                        customerId,
                        correlationId,
                        offset: 0,
                        limit: PageSize,
                        cancellationToken);

            ReplaceNotifications(
                page.Items);

            _nextOffset =
                page.Offset
                + page.Items.Count;

            HasMore =
                page.HasMore;

            HasLoaded =
                true;

            StatusText =
                BuildLoadedStatus();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            StatusText =
                "Notifications load canceled.";
        }
        catch (Exception exception)
        {
            HandleFailure(
                exception);
        }
        finally
        {
            IsLoading =
                false;

            NotifyCollectionStateChanged();
        }
    }

    [RelayCommand]
    private async Task LoadMoreNotificationsAsync(
        CancellationToken cancellationToken)
    {
        if (!CanLoadMore)
        {
            return;
        }

        if (!TryGetFilters(
                out Guid? orderId,
                out string? customerId,
                out Guid? correlationId))
        {
            return;
        }

        IsLoading =
            true;

        ErrorMessage =
            null;

        StatusText =
            "Loading more notifications...";

        try
        {
            OperationalNotificationPageDto page =
                await _notificationsApiClient
                    .GetNotificationsAsync(
                        orderId,
                        customerId,
                        correlationId,
                        _nextOffset,
                        PageSize,
                        cancellationToken);

            AppendNotifications(
                page.Items);

            _nextOffset =
                page.Offset
                + page.Items.Count;

            HasMore =
                page.HasMore;

            StatusText =
                BuildLoadedStatus();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            StatusText =
                "Notifications load canceled.";
        }
        catch (Exception exception)
        {
            HandleFailure(
                exception);
        }
        finally
        {
            IsLoading =
                false;

            NotifyCollectionStateChanged();
        }
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync(
        CancellationToken cancellationToken)
    {
        await LoadNotificationsAsync(
            cancellationToken);
    }

    [RelayCommand]
    private void ClearFilters()
    {
        OrderIdText =
            string.Empty;

        CustomerIdText =
            string.Empty;

        CorrelationIdText =
            string.Empty;
    }

    private bool TryGetFilters(
        out Guid? orderId,
        out string? customerId,
        out Guid? correlationId)
    {
        ErrorMessage =
            null;

        orderId =
            null;

        customerId =
            null;

        correlationId =
            null;

        string normalizedOrderId =
            OrderIdText.Trim();

        if (normalizedOrderId.Length > 0)
        {
            if (!Guid.TryParse(
                    normalizedOrderId,
                    out Guid parsedOrderId)
                || parsedOrderId == Guid.Empty)
            {
                ErrorMessage =
                    "Order ID must be a valid non-empty GUID.";

                return false;
            }

            orderId =
                parsedOrderId;
        }

        string normalizedCustomerId =
            CustomerIdText.Trim();

        if (normalizedCustomerId.Length > 0)
        {
            customerId =
                normalizedCustomerId;
        }

        string normalizedCorrelationId =
            CorrelationIdText.Trim();

        if (normalizedCorrelationId.Length > 0)
        {
            if (!Guid.TryParse(
                    normalizedCorrelationId,
                    out Guid parsedCorrelationId)
                || parsedCorrelationId == Guid.Empty)
            {
                ErrorMessage =
                    "Correlation ID must be a valid non-empty GUID.";

                return false;
            }

            correlationId =
                parsedCorrelationId;
        }

        return true;
    }

    private void ReplaceNotifications(
        IReadOnlyList<OperationalNotificationDto>
            notifications)
    {
        Notifications.Clear();

        foreach (
            OperationalNotificationDto notification
            in notifications)
        {
            Notifications.Add(
                notification);
        }

        SelectedNotification =
            null;

        NotifyCollectionStateChanged();
    }

    private void AppendNotifications(
        IReadOnlyList<OperationalNotificationDto>
            notifications)
    {
        HashSet<Guid> existingIds =
            Notifications
                .Select(
                    notification =>
                        notification.Id)
                .ToHashSet();

        foreach (
            OperationalNotificationDto notification
            in notifications)
        {
            if (existingIds.Add(
                    notification.Id))
            {
                Notifications.Add(
                    notification);
            }
        }

        NotifyCollectionStateChanged();
    }

    private string BuildLoadedStatus()
    {
        return HasMore
            ? $"{Notifications.Count} notification(s) loaded. More notifications are available."
            : $"{Notifications.Count} notification(s) loaded.";
    }

    private void HandleFailure(
        Exception exception)
    {
        ErrorMessage =
            exception switch
            {
                UnauthorizedAccessException
                    => "Authentication is required to access Notifications.",

                ApiRequestException apiException
                    when apiException.StatusCode
                         == HttpStatusCode.Unauthorized
                    => "Your authentication session expired. Sign in again.",

                ApiRequestException apiException
                    when apiException.StatusCode
                         == HttpStatusCode.Forbidden
                    => "Your account does not have permission to access Notifications.",

                OperationCanceledException
                    => "The Notifications request timed out.",

                HttpRequestException
                    => "The API Gateway could not be reached.",

                _
                    => "An unexpected error occurred while loading Notifications."
            };

        StatusText =
            "Notifications load failed.";

        if (exception
            is not UnauthorizedAccessException
            and not ApiRequestException
            and not OperationCanceledException
            and not HttpRequestException)
        {
            LogUnexpectedFailure(
                _logger,
                exception);
        }
    }

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(
            nameof(HasNotifications));

        OnPropertyChanged(
            nameof(IsEmpty));

        OnPropertyChanged(
            nameof(CanLoadMore));
    }

    [LoggerMessage(
        EventId = 5600,
        Level = LogLevel.Error,
        Message =
            "An unexpected error occurred while loading operational Notifications.")]
    private static partial void LogUnexpectedFailure(
        ILogger logger,
        Exception exception);
}