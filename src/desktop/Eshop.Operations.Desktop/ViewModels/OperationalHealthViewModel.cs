using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.OperationalHealth;

using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.ViewModels;

public sealed partial class OperationalHealthViewModel(
    IOperationalHealthApiClient healthApiClient,
    ILogger<OperationalHealthViewModel> logger)
    : ObservableObject
{
    public ObservableCollection<
        OperationalServiceHealthDto>
        Services
    {
        get;
    } = [];

    [ObservableProperty]
    public partial OperationalServiceHealthDto?
        SelectedService
    {
        get;
        set;
    }

    public bool HasLoaded =>
        CheckedAtUtc is not null;

    public bool HasServices =>
        Services.Count > 0;

    public bool HasError =>
        !string.IsNullOrWhiteSpace(
            ErrorMessage);

    [ObservableProperty]
    public partial bool IsLoading
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string OverallStatus
    {
        get;
        private set;
    } = "Not checked";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoaded))]
    public partial DateTimeOffset? CheckedAtUtc
    {
        get;
        private set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
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
    } = "Operational health not checked.";

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RefreshHealthAsync(
        CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading =
            true;

        ErrorMessage =
            null;

        StatusText =
            "Checking operational health...";

        try
        {
            OperationalHealthResponseDto response =
                await healthApiClient
                    .GetOperationalHealthAsync(
                        cancellationToken);

            Services.Clear();

            foreach (
                OperationalServiceHealthDto service
                in response.Services)
            {
                Services.Add(
                    service);
            }

            OverallStatus =
                response.Status;

            CheckedAtUtc =
                response.CheckedAtUtc;

            StatusText =
                response.Status == "Healthy"
                    ? "All monitored services are healthy."
                    : "One or more monitored services require attention.";

            OnPropertyChanged(
                nameof(HasServices));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            StatusText =
                "Operational health check canceled.";
        }
        catch (UnauthorizedAccessException)
        {
            SetError(
                "Sign in to check operational health.");
        }
        catch (ApiRequestException exception)
            when (exception.StatusCode
                  == HttpStatusCode.Unauthorized)
        {
            SetError(
                "Your authentication session expired. Sign in again.");
        }
        catch (ApiRequestException exception)
            when (exception.StatusCode
                  == HttpStatusCode.Forbidden)
        {
            SetError(
                "Support or admin access is required to check operational health.");
        }
        catch (ApiRequestException)
        {
            SetError(
                "The operational health request failed.");
        }
        catch (OperationCanceledException)
        {
            SetError(
                "The operational health request timed out.");
        }
        catch (HttpRequestException)
        {
            SetError(
                "The API Gateway could not be reached.");
        }
        catch (Exception exception)
        {
            LogUnexpectedFailure(
                logger,
                exception);

            SetError(
                "An unexpected error occurred while checking operational health.");
        }
        finally
        {
            IsLoading =
                false;
        }
    }

    private void SetError(
        string message)
    {
        ErrorMessage =
            message;

        StatusText =
            "Operational health check failed.";
    }

    [LoggerMessage(
        EventId = 5710,
        Level = LogLevel.Error,
        Message =
            "An unexpected error occurred while checking operational health.")]
    private static partial void LogUnexpectedFailure(
        ILogger logger,
        Exception exception);
}
