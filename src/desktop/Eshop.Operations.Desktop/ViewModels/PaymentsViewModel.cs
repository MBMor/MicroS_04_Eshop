using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Payments;
using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class PaymentsViewModel : ObservableObject
{
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
    }

    public ObservableCollection<PaymentDto> Payments { get; } = [];

    public bool HasPayments => Payments.Count > 0;

    public bool IsEmpty =>
        HasLoaded
        && !IsLoading
        && ErrorMessage is null
        && Payments.Count == 0;

    public bool HasError =>
        !string.IsNullOrWhiteSpace(ErrorMessage);

    [ObservableProperty]
    public partial PaymentDto? SelectedPayment { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool HasLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } =
        "Payments not loaded.";

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

    private void ReplacePayments(
        IReadOnlyList<PaymentDto> payments)
    {
        Guid? selectedId = SelectedPayment?.Id;

        Payments.Clear();
        foreach (PaymentDto payment in payments)
        {
            Payments.Add(payment);
        }

        SelectedPayment = selectedId is null
            ? null
            : Payments.FirstOrDefault(
                payment => payment.Id == selectedId.Value);

        OnPropertyChanged(nameof(HasPayments));
        OnPropertyChanged(nameof(IsEmpty));
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
