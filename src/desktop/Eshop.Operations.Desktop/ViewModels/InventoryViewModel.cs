using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Inventory;
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
    }

    public ObservableCollection<InventoryItemDto> Items { get; } = [];

    public bool HasItems =>
        Items.Count > 0;

    public bool IsEmpty =>
        HasLoaded
        && !IsLoading
        && ErrorMessage is null
        && Items.Count == 0;

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
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool HasLoaded
    {
        get;
        private set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading
    {
        get;
        private set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
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

        SelectedItem =
            selectedId is null
                ? null
                : Items.FirstOrDefault(
                    item =>
                        item.Id == selectedId.Value);

        OnPropertyChanged(
            nameof(HasItems));

        OnPropertyChanged(
            nameof(IsEmpty));
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
