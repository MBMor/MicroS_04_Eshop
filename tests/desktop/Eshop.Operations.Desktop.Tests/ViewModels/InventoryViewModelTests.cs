using System.Globalization;
using Eshop.Operations.Desktop.Api.Inventory;
using Eshop.Operations.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.Net;
using Eshop.Operations.Desktop.Api;


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

    private static InventoryViewModel CreateViewModel(
    IInventoryApiClient apiClient)
    {
        return new InventoryViewModel(
            apiClient,
            NullLogger<InventoryViewModel>.Instance);
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
            null);
    }

    private sealed class StubInventoryApiClient(
        Func<
            bool,
            CancellationToken,
            Task<IReadOnlyList<InventoryItemDto>>> getItems)
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
    }

}
