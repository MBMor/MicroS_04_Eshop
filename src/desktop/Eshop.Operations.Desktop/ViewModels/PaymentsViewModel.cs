using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Payments;
using Eshop.Operations.Desktop.Models;
using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class PaymentsViewModel : ObservableObject
{
    private const string AllStatusesLabel =
        "All statuses";

    private readonly IPaymentsApiClient _paymentsApiClient;
    private readonly ILogger<PaymentsViewModel> _logger;

    public PaymentsViewModel(
        IPaymentsApiClient paymentsApiClient,
        ILogger<PaymentsViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(paymentsApiClient);
        ArgumentNullException.ThrowIfNull(logger);

        _paymentsApiClient = paymentsApiClient;
        _logger = logger;

        PaymentsView =
            CollectionViewSource.GetDefaultView(
                Payments);

        PaymentsView.Filter =
            FilterPayment;

        SelectedSortOption =
            SortOptions[0];
    }

    public ObservableCollection<PaymentDto> Payments { get; } = [];

    public ICollectionView PaymentsView { get; }

    public ObservableCollection<string> Statuses { get; } =
    [
        AllStatusesLabel
    ];

    public IReadOnlyList<ListSortOption> SortOptions { get; } =
    [
        new(
            "Created newest first",
            nameof(PaymentDto.CreatedAtUtc),
            ListSortDirection.Descending),

        new(
            "Created oldest first",
            nameof(PaymentDto.CreatedAtUtc),
            ListSortDirection.Ascending),

        new(
            "Amount high to low",
            nameof(PaymentDto.Amount),
            ListSortDirection.Descending),

        new(
            "Amount low to high",
            nameof(PaymentDto.Amount),
            ListSortDirection.Ascending),

        new(
            "Status A–Z",
            nameof(PaymentDto.Status),
            ListSortDirection.Ascending)
    ];

    public bool HasPayments => Payments.Count > 0;

    public bool HasVisiblePayments =>
        !PaymentsView.IsEmpty;

    public bool IsInitialState =>
        !HasLoaded
        && !IsLoading
        && ErrorMessage is null;

    public bool IsEmpty =>
        HasLoaded
        && !IsLoading
        && ErrorMessage is null
        && Payments.Count == 0;

    public bool IsFilteredEmpty =>
        HasLoaded
        && Payments.Count > 0
        && !IsLoading
        && ErrorMessage is null
        && PaymentsView.IsEmpty;

    public bool HasError =>
        !string.IsNullOrWhiteSpace(ErrorMessage);

    [ObservableProperty]
    public partial PaymentDto? SelectedPayment { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedStatus { get; set; } =
        AllStatusesLabel;

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
    public partial string StatusText { get; private set; } =
        "Payments not loaded.";

    partial void OnSearchTextChanged(string value)
    {
        RefreshPaymentsView();
    }

    partial void OnSelectedStatusChanged(string value)
    {
        RefreshPaymentsView();
    }

    partial void OnSelectedSortOptionChanged(
        ListSortOption? value)
    {
        if (value is not null)
        {
            ApplySort(value);
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
        SearchText = orderId.ToString("D");

        if (!HasLoaded && !IsLoading)
        {
            await LoadPaymentsAsync(cancellationToken);
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LoadPaymentsAsync(
        CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        StatusText = HasLoaded
            ? "Refreshing payments..."
            : "Loading payments...";

        try
        {
            IReadOnlyList<PaymentDto> payments =
                await _paymentsApiClient.GetPaymentsAsync(
                    cancellationToken);

            ReplacePayments(payments);
            HasLoaded = true;
            StatusText = $"{Payments.Count} payment(s) loaded.";
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            StatusText = HasLoaded
                ? "Payments refresh canceled."
                : "Payments load canceled.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage =
                "Authentication is required to load Payments.";
            StatusText = "Payments load failed.";
        }
        catch (ApiRequestException exception)
            when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            ErrorMessage =
                "Your authentication session expired. Sign in again.";
            StatusText = "Payments load failed.";
        }
        catch (ApiRequestException exception)
            when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            ErrorMessage =
                "Your account does not have permission to access Payments.";
            StatusText = "Payments load failed.";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "The Payments request timed out.";
            StatusText = "Payments load failed.";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "The API Gateway could not be reached.";
            StatusText = "Payments load failed.";
        }
        catch (Exception exception)
        {
            LogUnexpectedPaymentsFailure(_logger, exception);
            ErrorMessage =
                "An unexpected error occurred while loading Payments.";
            StatusText = "Payments load failed.";
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
        SelectedStatus = AllStatusesLabel;
        SelectedSortOption = SortOptions[0];
    }

    private bool FilterPayment(object item)
    {
        if (item is not PaymentDto payment)
        {
            return false;
        }

        if (!string.Equals(
                SelectedStatus,
                AllStatusesLabel,
                StringComparison.Ordinal)
            && !string.Equals(
                payment.Status,
                SelectedStatus,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string searchText = SearchText.Trim();
        if (searchText.Length == 0)
        {
            return true;
        }

        return payment.CustomerId.Contains(
                   searchText,
                   StringComparison.CurrentCultureIgnoreCase)
               || payment.PaymentMethod.Contains(
                   searchText,
                   StringComparison.CurrentCultureIgnoreCase)
               || payment.Status.Contains(
                   searchText,
                   StringComparison.CurrentCultureIgnoreCase)
               || payment.OrderId.ToString().Contains(
                   searchText,
                   StringComparison.OrdinalIgnoreCase)
               || payment.Id.ToString().Contains(
                   searchText,
                   StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshPaymentsView()
    {
        PaymentsView.Refresh();

        if (SelectedPayment is not null
            && !PaymentsView.Contains(SelectedPayment))
        {
            SelectedPayment = null;
        }

        OnPropertyChanged(nameof(HasVisiblePayments));
        OnPropertyChanged(nameof(IsFilteredEmpty));
    }

    private void ApplySort(ListSortOption sortOption)
    {
        PaymentsView.SortDescriptions.Clear();
        PaymentsView.SortDescriptions.Add(
            new SortDescription(
                sortOption.PropertyName,
                sortOption.Direction));
    }

    private void RebuildStatuses()
    {
        string previousStatus = SelectedStatus;

        Statuses.Clear();
        Statuses.Add(AllStatusesLabel);

        IEnumerable<string> statuses = Payments
            .Select(payment => payment.Status)
            .Where(status => !string.IsNullOrWhiteSpace(status))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(
                status => status,
                StringComparer.CurrentCultureIgnoreCase);

        foreach (string status in statuses)
        {
            Statuses.Add(status);
        }

        bool statusStillExists = Statuses.Any(
            status => string.Equals(
                status,
                previousStatus,
                StringComparison.OrdinalIgnoreCase));

        SelectedStatus = statusStillExists
            ? previousStatus
            : AllStatusesLabel;
    }

    private void ReplacePayments(
        IReadOnlyList<PaymentDto> payments)
    {
        Guid? selectedId = SelectedPayment?.Id;

        Payments.Clear();
        foreach (PaymentDto payment in payments)
        {
            Payments.Add(payment);
        }

        RebuildStatuses();
        PaymentsView.Refresh();

        PaymentDto? refreshedSelection =
            selectedId is null
                ? null
                : Payments.FirstOrDefault(
                    payment => payment.Id == selectedId.Value);

        SelectedPayment =
            refreshedSelection is not null
            && PaymentsView.Contains(refreshedSelection)
                ? refreshedSelection
                : null;

        OnPropertyChanged(nameof(HasPayments));
        OnPropertyChanged(nameof(HasVisiblePayments));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsFilteredEmpty));
    }

    [LoggerMessage(
        EventId = 5300,
        Level = LogLevel.Error,
        Message =
            "An unexpected error occurred while loading Payments.")]
    private static partial void LogUnexpectedPaymentsFailure(
        ILogger logger,
        Exception exception);
}
