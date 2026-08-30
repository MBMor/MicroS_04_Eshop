using System.Globalization;
using Eshop.Operations.Desktop.Api.Inventory;
using Eshop.Operations.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.Net;
using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Authentication;
using Eshop.Operations.Desktop.Models;
using Eshop.Operations.Desktop.Services;


namespace Eshop.Operations.Desktop.Tests.ViewModels;

public sealed class InventoryViewModelTests
{
    [Fact]
    public async Task LoadInventoryCommandLoadsInventoryItems()
    {
        InventoryItemDto item =
            CreateItem();

        var apiClient =
            new StubInventoryApiClient(
                (_, _) =>
                    Task.FromResult<
                        IReadOnlyList<InventoryItemDto>>(
                        [item]));

        InventoryViewModel viewModel =
            CreateViewModel(
                apiClient);

        await viewModel
            .LoadInventoryCommand
            .ExecuteAsync(null);

        Assert.Same(
            item,
            Assert.Single(
                viewModel.Items));

        Assert.True(
            viewModel.HasLoaded);

        Assert.Null(
            viewModel.ErrorMessage);
    }

    [Fact]
    public async Task LoadInventoryCommandMapsForbiddenResponse()
    {
        var apiClient =
            new StubInventoryApiClient(
                (_, _) =>
                    Task.FromException<
                        IReadOnlyList<InventoryItemDto>>(
                        new ApiRequestException(
                            HttpStatusCode.Forbidden,
                            null)));

        InventoryViewModel viewModel =
            CreateViewModel(
                apiClient);

        await viewModel
            .LoadInventoryCommand
            .ExecuteAsync(null);

        Assert.Equal(
            "Your account does not have permission to access Inventory.",
            viewModel.ErrorMessage);
    }

    [Fact]
    public async Task FailedRefreshKeepsPreviouslyLoadedItems()
    {
        InventoryItemDto item =
            CreateItem();

        var callCount = 0;

        var apiClient =
            new StubInventoryApiClient(
                (_, _) =>
                {
                    callCount++;

                    return callCount == 1
                        ? Task.FromResult<
                            IReadOnlyList<InventoryItemDto>>(
                            [item])
                        : Task.FromException<
                            IReadOnlyList<InventoryItemDto>>(
                            new HttpRequestException(
                                "Gateway unavailable."));
                });

        InventoryViewModel viewModel =
            CreateViewModel(
                apiClient);

        await viewModel
            .LoadInventoryCommand
            .ExecuteAsync(null);

        await viewModel
            .LoadInventoryCommand
            .ExecuteAsync(null);

        Assert.Same(
            item,
            Assert.Single(
                viewModel.Items));

        Assert.NotNull(
            viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SearchTextFiltersInventoryBySku()
    {
        InventoryItemDto keyboard = CreateItem() with { Sku = "KEYBOARD" };
        InventoryItemDto mouse = CreateItem() with { Sku = "MOUSE" };
        InventoryViewModel viewModel = CreateViewModel(
            new StubInventoryApiClient(
                (_, _) => Task.FromResult<IReadOnlyList<InventoryItemDto>>(
                    [keyboard, mouse])));

        await viewModel.LoadInventoryCommand.ExecuteAsync(null);
        viewModel.SearchText = "mouse";

        InventoryItemDto visible = Assert.Single(
            viewModel.ItemsView.Cast<InventoryItemDto>());
        Assert.Same(mouse, visible);
    }

    [Fact]
    public async Task FilteringOutSelectedItemClearsSelection()
    {
        InventoryItemDto item = CreateItem();
        InventoryViewModel viewModel = CreateViewModel(
            new StubInventoryApiClient(
                (_, _) => Task.FromResult<IReadOnlyList<InventoryItemDto>>(
                    [item])));

        await viewModel.LoadInventoryCommand.ExecuteAsync(null);
        viewModel.SelectedItem = item;
        viewModel.SearchText = "does-not-match";

        Assert.Null(viewModel.SelectedItem);
        Assert.True(viewModel.IsFilteredEmpty);
    }

    [Fact]
    public async Task SelectedSortOptionSortsInventoryView()
    {
        InventoryItemDto low = CreateItem() with
        {
            Sku = "LOW",
            AvailableQuantity = 2
        };
        InventoryItemDto high = CreateItem() with
        {
            Sku = "HIGH",
            AvailableQuantity = 20
        };
        InventoryViewModel viewModel = CreateViewModel(
            new StubInventoryApiClient(
                (_, _) => Task.FromResult<IReadOnlyList<InventoryItemDto>>(
                    [high, low])));

        await viewModel.LoadInventoryCommand.ExecuteAsync(null);
        viewModel.SelectedSortOption = viewModel.SortOptions
            .Single(option => option.DisplayName == "Available low to high");

        Assert.Equal(
            ["LOW", "HIGH"],
            viewModel.ItemsView.Cast<InventoryItemDto>()
                .Select(item => item.Sku)
                .ToArray());
    }

    [Fact]
    public async Task AdjustSelectedStockCommandUsesSelectedVersionAndUpdatesItem()
    {
        InventoryItemDto original = CreateItem();
        InventoryItemDto adjusted = original with
        {
            OnHandQuantity = 15,
            AvailableQuantity = 10,
            UpdatedAtUtc = original.CreatedAtUtc.AddMinutes(1),
            Version = 43
        };
        InventoryStockAdjustmentRequest? capturedRequest = null;
        var apiClient = new StubInventoryApiClient(
            (_, _) => Task.FromResult<IReadOnlyList<InventoryItemDto>>([original]),
            (request, _) =>
            {
                capturedRequest = request;
                return Task.FromResult(new InventoryStockAdjustmentResult(adjusted, false, request.IdempotencyKey));
            });
        var dialogService = new StubInventoryStockAdjustmentDialogService(
            new InventoryStockAdjustmentDraft(-5, "Physical stock correction"));
        InventoryViewModel viewModel = CreateViewModel(apiClient, CreateAdminAuthentication(), dialogService);
        await viewModel.LoadInventoryCommand.ExecuteAsync(null);
        viewModel.SelectedItem = original;
        await viewModel.AdjustSelectedStockCommand.ExecuteAsync(null);

        Assert.NotNull(capturedRequest);
        Assert.Equal(original.Id, capturedRequest.InventoryItemId);
        Assert.Equal(-5, capturedRequest.QuantityDelta);
        Assert.Equal(42u, capturedRequest.ExpectedVersion);
        Assert.Equal("Physical stock correction", capturedRequest.Reason);
        Assert.NotEqual(Guid.Empty, capturedRequest.IdempotencyKey);
        Assert.Same(adjusted, Assert.Single(viewModel.Items));
        Assert.Same(adjusted, viewModel.SelectedItem);
        Assert.Equal(43u, viewModel.SelectedItem.Version);
        Assert.False(viewModel.HasUnknownAdjustmentOutcome);
    }

    [Fact]
    public async Task UnknownOutcomeRetryUsesExactlyTheSameStockAdjustmentRequest()
    {
        InventoryItemDto original = CreateItem();
        InventoryItemDto adjusted = original with { OnHandQuantity = 22, AvailableQuantity = 17, Version = 43 };
        var capturedRequests = new List<InventoryStockAdjustmentRequest>();
        var callCount = 0;
        var apiClient = new StubInventoryApiClient(
            (_, _) => Task.FromResult<IReadOnlyList<InventoryItemDto>>([original]),
            (request, _) =>
            {
                capturedRequests.Add(request);
                callCount++;
                if (callCount == 1)
                {
                    throw new InventoryStockAdjustmentOutcomeUnknownException(
                        request.IdempotencyKey, "Unknown outcome.", new HttpRequestException("Connection reset."));
                }
                return Task.FromResult(new InventoryStockAdjustmentResult(adjusted, true, request.IdempotencyKey));
            });
        var dialogService = new StubInventoryStockAdjustmentDialogService(
            new InventoryStockAdjustmentDraft(2, "Physical stock correction"));
        InventoryViewModel viewModel = CreateViewModel(apiClient, CreateAdminAuthentication(), dialogService);
        await viewModel.LoadInventoryCommand.ExecuteAsync(null);
        viewModel.SelectedItem = original;
        await viewModel.AdjustSelectedStockCommand.ExecuteAsync(null);
        Assert.True(viewModel.HasUnknownAdjustmentOutcome);
        Assert.False(viewModel.CanStartStockAdjustment);
        Assert.NotNull(viewModel.UnknownOutcomeOperationId);
        await viewModel.RetryUnknownStockAdjustmentCommand.ExecuteAsync(null);
        Assert.Equal(2, capturedRequests.Count);
        Assert.Equal(capturedRequests[0], capturedRequests[1]);
        Assert.Equal(capturedRequests[0].IdempotencyKey, capturedRequests[1].IdempotencyKey);
        Assert.False(viewModel.HasUnknownAdjustmentOutcome);
        Assert.Same(adjusted, viewModel.SelectedItem);
        Assert.Equal("Stock adjustment confirmed by idempotent replay.", viewModel.StatusText);
    }

    [Fact]
    public async Task SupportUserCannotStartStockAdjustment()
    {
        InventoryItemDto item = CreateItem();
        var authentication = new AuthenticationState(
            new AuthenticatedUser("support-123", "sam.support", "sam.support@eshop.local", ["support"]));
        var dialogService = new StubInventoryStockAdjustmentDialogService(
            new InventoryStockAdjustmentDraft(1, "Physical stock correction"));
        InventoryViewModel viewModel = CreateViewModel(
            new StubInventoryApiClient((_, _) => Task.FromResult<IReadOnlyList<InventoryItemDto>>([item])),
            authentication,
            dialogService);
        await viewModel.LoadInventoryCommand.ExecuteAsync(null);
        viewModel.SelectedItem = item;
        Assert.False(viewModel.CanStartStockAdjustment);
        await viewModel.AdjustSelectedStockCommand.ExecuteAsync(null);
        Assert.Equal(0, dialogService.CallCount);
        Assert.Equal("Only an administrator can adjust inventory stock.", viewModel.AdjustmentErrorMessage);
    }

    private static InventoryViewModel CreateViewModel(
        IInventoryApiClient apiClient,
        AuthenticationState? authentication = null,
        IInventoryStockAdjustmentDialogService? dialogService = null)
    {
        authentication ??= new AuthenticationState();
        dialogService ??= new StubInventoryStockAdjustmentDialogService(null);

        return new InventoryViewModel(
            apiClient,
            authentication,
            dialogService,
            NullLogger<InventoryViewModel>.Instance);
    }

    private static AuthenticationState CreateAdminAuthentication()
    {
        return new AuthenticationState(
            new AuthenticatedUser(
                "admin-123",
                "anna.admin",
                "anna.admin@eshop.local",
                ["admin"]));
    }

    private static InventoryItemDto CreateItem()
    {
        return new InventoryItemDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "KEY-001",
            20,
            5,
            15,
            true,
            DateTimeOffset.Parse(
                "2026-08-01T10:00:00+00:00",
                CultureInfo.InvariantCulture),
            null,
            42);
    }

    private sealed class StubInventoryApiClient(
        Func<
            bool,
            CancellationToken,
            Task<IReadOnlyList<InventoryItemDto>>> getItems,
        Func<
            InventoryStockAdjustmentRequest,
            CancellationToken,
            Task<InventoryStockAdjustmentResult>>? adjustStock = null)
        : IInventoryApiClient
    {
        public Task<IReadOnlyList<InventoryItemDto>>
            GetInventoryItemsAsync(
                bool includeInactive,
                CancellationToken cancellationToken)
        {
            return getItems(
                includeInactive,
                cancellationToken);
        }

        public Task<InventoryStockAdjustmentResult>
            AdjustStockAsync(
                InventoryStockAdjustmentRequest request,
                CancellationToken cancellationToken)
        {
            if (adjustStock is null)
            {
                throw new InvalidOperationException(
                    "Stock adjustment was not expected in this test.");
            }

            return adjustStock(request, cancellationToken);
        }
    }

    private sealed class StubInventoryStockAdjustmentDialogService(
        InventoryStockAdjustmentDraft? result)
        : IInventoryStockAdjustmentDialogService
    {
        public InventoryItemDto? LastItem { get; private set; }
        public int CallCount { get; private set; }

        public InventoryStockAdjustmentDraft? ShowConfirmation(InventoryItemDto item)
        {
            CallCount++;
            LastItem = item;
            return result;
        }
    }

}
