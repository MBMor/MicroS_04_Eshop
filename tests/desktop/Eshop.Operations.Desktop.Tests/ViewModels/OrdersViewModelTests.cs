using System.Globalization;
using System.Net;

using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Orders;
using Eshop.Operations.Desktop.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Eshop.Operations.Desktop.Tests.ViewModels;

public sealed class OrdersViewModelTests
{
    [Fact]
    public async Task LoadOrdersCommandLoadsFirstBoundedPage()
    {
        OperationalOrderSummaryDto order = CreateOrder();
        int? capturedOffset = null;
        int? capturedLimit = null;

        OrdersViewModel viewModel = CreateViewModel(new StubOrdersApiClient(
            (offset, limit, _) =>
            {
                capturedOffset = offset;
                capturedLimit = limit;
                return Task.FromResult(new OperationalOrderPageDto([order], offset, limit, true));
            }));

        await viewModel.LoadOrdersCommand.ExecuteAsync(null);

        Assert.Equal(0, capturedOffset);
        Assert.Equal(25, capturedLimit);
        Assert.Same(order, Assert.Single(viewModel.Orders));
        Assert.True(viewModel.HasLoaded);
        Assert.True(viewModel.HasMore);
        Assert.True(viewModel.CanLoadMore);
    }

    [Fact]
    public async Task LoadMoreOrdersCommandUsesNextOffsetAndAppends()
    {
        OperationalOrderSummaryDto[] firstPage = Enumerable.Range(1, 25).Select(CreateOrder).ToArray();
        OperationalOrderSummaryDto nextOrder = CreateOrder(26);
        var requestedOffsets = new List<int>();

        OrdersViewModel viewModel = CreateViewModel(new StubOrdersApiClient(
            (offset, limit, _) =>
            {
                requestedOffsets.Add(offset);
                return Task.FromResult(offset == 0
                    ? new OperationalOrderPageDto(firstPage, 0, limit, true)
                    : new OperationalOrderPageDto([nextOrder], offset, limit, false));
            }));

        await viewModel.LoadOrdersCommand.ExecuteAsync(null);
        await viewModel.LoadMoreOrdersCommand.ExecuteAsync(null);

        Assert.Equal([0, 25], requestedOffsets);
        Assert.Equal(26, viewModel.Orders.Count);
        Assert.False(viewModel.HasMore);
        Assert.False(viewModel.CanLoadMore);
    }

    [Fact]
    public async Task SearchAndStatusFilterLoadedOrders()
    {
        OperationalOrderSummaryDto pending = CreateOrder(1) with { CustomerEmail = "alice@example.com", Status = "Pending" };
        OperationalOrderSummaryDto confirmed = CreateOrder(2) with { CustomerEmail = "bob@example.com", Status = "Confirmed" };

        OrdersViewModel viewModel = CreateViewModel(new StubOrdersApiClient(
            (offset, limit, _) => Task.FromResult(new OperationalOrderPageDto([pending, confirmed], offset, limit, false))));

        await viewModel.LoadOrdersCommand.ExecuteAsync(null);
        viewModel.SearchText = "bob";
        Assert.Same(confirmed, Assert.Single(viewModel.OrdersView.Cast<OperationalOrderSummaryDto>()));

        viewModel.SearchText = string.Empty;
        viewModel.SelectedStatus = "Pending";
        Assert.Same(pending, Assert.Single(viewModel.OrdersView.Cast<OperationalOrderSummaryDto>()));
    }

    [Fact]
    public async Task LoadOrderDetailAsyncLoadsSelectedOrderOnly()
    {
        OperationalOrderSummaryDto summary = CreateOrder();
        OperationalOrderDetailDto detail = CreateDetail(summary.Id);
        Guid? capturedOrderId = null;

        OrdersViewModel viewModel = CreateViewModel(new StubOrdersApiClient(
            (offset, limit, _) => Task.FromResult(new OperationalOrderPageDto([summary], offset, limit, false)),
            (orderId, _) =>
            {
                capturedOrderId = orderId;
                return Task.FromResult(detail);
            }));

        await viewModel.LoadOrdersCommand.ExecuteAsync(null);
        viewModel.SelectedOrder = summary;
        await viewModel.LoadOrderDetailAsync(summary);

        Assert.Equal(summary.Id, capturedOrderId);
        Assert.Same(detail, viewModel.SelectedOrderDetail);
        Assert.Null(viewModel.DetailErrorMessage);
        Assert.False(viewModel.IsDetailLoading);
    }

    [Fact]
    public async Task DetailFailureKeepsSummarySelection()
    {
        OperationalOrderSummaryDto summary = CreateOrder();
        OrdersViewModel viewModel = CreateViewModel(new StubOrdersApiClient(
            (offset, limit, _) => Task.FromResult(new OperationalOrderPageDto([summary], offset, limit, false)),
            (_, _) => Task.FromException<OperationalOrderDetailDto>(new ApiRequestException(HttpStatusCode.Forbidden, null))));

        await viewModel.LoadOrdersCommand.ExecuteAsync(null);
        viewModel.SelectedOrder = summary;
        await viewModel.LoadOrderDetailAsync(summary);

        Assert.Same(summary, viewModel.SelectedOrder);
        Assert.Null(viewModel.SelectedOrderDetail);
        Assert.Equal("Your account does not have permission to access Orders.", viewModel.DetailErrorMessage);
    }

    [Fact]
    public async Task FilteringOutSelectedOrderClearsSelectionAndDetail()
    {
        OperationalOrderSummaryDto summary = CreateOrder();
        OperationalOrderDetailDto detail = CreateDetail(summary.Id);
        OrdersViewModel viewModel = CreateViewModel(new StubOrdersApiClient(
            (offset, limit, _) => Task.FromResult(new OperationalOrderPageDto([summary], offset, limit, false)),
            (_, _) => Task.FromResult(detail)));

        await viewModel.LoadOrdersCommand.ExecuteAsync(null);
        viewModel.SelectedOrder = summary;
        await viewModel.LoadOrderDetailAsync(summary);
        viewModel.SearchText = "does-not-match";

        Assert.Null(viewModel.SelectedOrder);
        Assert.Null(viewModel.SelectedOrderDetail);
        Assert.True(viewModel.IsFilteredEmpty);
    }

    [Fact]
    public async Task FocusOrderAsyncLoadsDetailWithoutLoadingSummaryPages()
    {
        Guid orderId = Guid.NewGuid();
        OperationalOrderDetailDto detail = CreateDetail(orderId);
        int listRequestCount = 0;
        Guid? capturedOrderId = null;

        OrdersViewModel viewModel = CreateViewModel(new StubOrdersApiClient(
            (offset, limit, _) =>
            {
                listRequestCount++;
                return Task.FromResult(new OperationalOrderPageDto([], offset, limit, false));
            },
            (requestedOrderId, _) =>
            {
                capturedOrderId = requestedOrderId;
                return Task.FromResult(detail);
            }));

        await viewModel.FocusOrderAsync(
            orderId,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, listRequestCount);
        Assert.Equal(orderId, capturedOrderId);
        Assert.Equal(orderId, viewModel.DetailOrderId);
        Assert.Null(viewModel.SelectedOrder);
        Assert.Same(detail, viewModel.SelectedOrderDetail);
        Assert.False(viewModel.IsDetailLoading);
    }

    [Fact]
    public async Task FocusOrderAsyncFiltersLoadedSummariesByOrderId()
    {
        OperationalOrderSummaryDto target = CreateOrder();
        OperationalOrderSummaryDto other = CreateOrder(2);
        OperationalOrderDetailDto detail = CreateDetail(target.Id);

        OrdersViewModel viewModel = CreateViewModel(new StubOrdersApiClient(
            (offset, limit, _) => Task.FromResult(
                new OperationalOrderPageDto([target, other], offset, limit, false)),
            (_, _) => Task.FromResult(detail)));

        await viewModel.LoadOrdersCommand.ExecuteAsync(null);
        await viewModel.FocusOrderAsync(
            target.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(target.Id.ToString("D"), viewModel.SearchText);
        OperationalOrderSummaryDto visible = Assert.Single(
            viewModel.OrdersView.Cast<OperationalOrderSummaryDto>());
        Assert.Same(target, visible);
        Assert.Equal(target.Id, viewModel.DetailOrderId);
        Assert.Same(detail, viewModel.SelectedOrderDetail);
    }

    private static OrdersViewModel CreateViewModel(IOrdersApiClient apiClient) =>
        new(apiClient, NullLogger<OrdersViewModel>.Instance);

    private static OperationalOrderSummaryDto CreateOrder(int sequence = 1) => new(
        Guid.NewGuid(),
        $"customer-{sequence}",
        $"customer-{sequence}@example.com",
        "Pending",
        1499.50m + sequence,
        "CZK",
        sequence,
        DateTimeOffset.Parse("2026-08-30T10:00:00+00:00", CultureInfo.InvariantCulture).AddMinutes(sequence),
        null);

    private static OperationalOrderDetailDto CreateDetail(Guid orderId) => new(
        orderId,
        "customer-123",
        "customer@example.com",
        "Confirmed",
        1499.50m,
        "CZK",
        "test-success",
        DateTimeOffset.Parse("2026-08-30T10:00:00+00:00", CultureInfo.InvariantCulture),
        DateTimeOffset.Parse("2026-08-30T10:02:00+00:00", CultureInfo.InvariantCulture),
        [new OperationalOrderItemDto(Guid.NewGuid(), Guid.NewGuid(), "Mechanical Keyboard", 499.50m, "CZK", 3, 1498.50m)],
        [
            new OperationalOrderStatusHistoryDto(null, "Pending", "Order created.", DateTimeOffset.Parse("2026-08-30T10:00:00+00:00", CultureInfo.InvariantCulture)),
            new OperationalOrderStatusHistoryDto("Pending", "Confirmed", "Order confirmed.", DateTimeOffset.Parse("2026-08-30T10:02:00+00:00", CultureInfo.InvariantCulture))
        ]);

    private sealed class StubOrdersApiClient(
        Func<int, int, CancellationToken, Task<OperationalOrderPageDto>> getOrders,
        Func<Guid, CancellationToken, Task<OperationalOrderDetailDto>>? getOrder = null) : IOrdersApiClient
    {
        public Task<OperationalOrderPageDto> GetOrdersAsync(int offset, int limit, CancellationToken cancellationToken) =>
            getOrders(offset, limit, cancellationToken);

        public Task<OperationalOrderDetailDto> GetOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
            getOrder?.Invoke(orderId, cancellationToken)
            ?? throw new InvalidOperationException("Order detail was not expected in this test.");
    }
}
