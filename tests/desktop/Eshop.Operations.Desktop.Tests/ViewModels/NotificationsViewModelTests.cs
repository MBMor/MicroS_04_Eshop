using Eshop.Operations.Desktop.Api.Notifications;
using Eshop.Operations.Desktop.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Eshop.Operations.Desktop.Tests.ViewModels;

public sealed class NotificationsViewModelTests
{
    [Fact]
    public async Task
        LoadNotificationsCommandLoadsFirstPage()
    {
        OperationalNotificationDto notification =
            CreateNotification();

        int? capturedOffset =
            null;

        int? capturedLimit =
            null;

        NotificationsViewModel viewModel =
            CreateViewModel(
                new StubNotificationsApiClient(
                    (orderId,
                     customerId,
                     correlationId,
                     offset,
                     limit,
                     _) =>
                    {
                        capturedOffset =
                            offset;

                        capturedLimit =
                            limit;

                        return Task.FromResult(
                            new OperationalNotificationPageDto(
                                [notification],
                                offset,
                                limit,
                                true));
                    }));

        await viewModel
            .LoadNotificationsCommand
            .ExecuteAsync(null);

        Assert.Equal(
            0,
            capturedOffset);

        Assert.Equal(
            25,
            capturedLimit);

        Assert.Same(
            notification,
            Assert.Single(
                viewModel.Notifications));

        Assert.True(
            viewModel.HasMore);

        Assert.True(
            viewModel.CanLoadMore);
    }

    [Fact]
    public async Task
        ApplyFiltersCommandPassesOrderAndCorrelationIdsToApi()
    {
        Guid orderId =
            Guid.NewGuid();

        Guid correlationId =
            Guid.NewGuid();

        Guid? capturedOrderId =
            null;

        Guid? capturedCorrelationId =
            null;

        string? capturedCustomerId =
            null;

        NotificationsViewModel viewModel =
            CreateViewModel(
                new StubNotificationsApiClient(
                    (requestedOrderId,
                     customerId,
                     requestedCorrelationId,
                     offset,
                     limit,
                     _) =>
                    {
                        capturedOrderId =
                            requestedOrderId;

                        capturedCustomerId =
                            customerId;

                        capturedCorrelationId =
                            requestedCorrelationId;

                        return Task.FromResult(
                            new OperationalNotificationPageDto(
                                [],
                                offset,
                                limit,
                                false));
                    }));

        viewModel.OrderIdText =
            orderId.ToString("D");

        viewModel.CustomerIdText =
            "customer-123";

        viewModel.CorrelationIdText =
            correlationId.ToString("D");

        await viewModel
            .ApplyFiltersCommand
            .ExecuteAsync(null);

        Assert.Equal(
            orderId,
            capturedOrderId);

        Assert.Equal(
            "customer-123",
            capturedCustomerId);

        Assert.Equal(
            correlationId,
            capturedCorrelationId);
    }

    [Fact]
    public async Task
        ApplyFiltersCommandRejectsInvalidOrderIdLocally()
    {
        var requestCount =
            0;

        NotificationsViewModel viewModel =
            CreateViewModel(
                new StubNotificationsApiClient(
                    (_, _, _, offset, limit, _) =>
                    {
                        requestCount++;

                        return Task.FromResult(
                            new OperationalNotificationPageDto(
                                [],
                                offset,
                                limit,
                                false));
                    }));

        viewModel.OrderIdText =
            "not-a-guid";

        await viewModel
            .ApplyFiltersCommand
            .ExecuteAsync(null);

        Assert.Equal(
            0,
            requestCount);

        Assert.Equal(
            "Order ID must be a valid non-empty GUID.",
            viewModel.ErrorMessage);
    }

    [Fact]
    public async Task
        LoadMoreNotificationsCommandUsesNextOffsetAndAppends()
    {
        OperationalNotificationDto first =
            CreateNotification();

        OperationalNotificationDto second =
            CreateNotification();

        var offsets =
            new List<int>();

        NotificationsViewModel viewModel =
            CreateViewModel(
                new StubNotificationsApiClient(
                    (_, _, _, offset, limit, _) =>
                    {
                        offsets.Add(offset);

                        return Task.FromResult(
                            offset == 0
                                ? new OperationalNotificationPageDto(
                                    [first],
                                    offset,
                                    limit,
                                    true)
                                : new OperationalNotificationPageDto(
                                    [second],
                                    offset,
                                    limit,
                                    false));
                    }));

        await viewModel
            .LoadNotificationsCommand
            .ExecuteAsync(null);

        await viewModel
            .LoadMoreNotificationsCommand
            .ExecuteAsync(null);

        Assert.Equal(
            [0, 1],
            offsets);

        Assert.Equal(
            [first, second],
            viewModel.Notifications);

        Assert.False(
            viewModel.HasMore);

        Assert.False(
            viewModel.CanLoadMore);
    }

    [Fact]
    public async Task
        FocusOrderAsyncLoadsNotificationsUsingOrderFilter()
    {
        Guid orderId =
            Guid.NewGuid();

        OperationalNotificationDto notification =
            CreateNotification() with
            {
                Id = Guid.NewGuid(),
                OrderId = orderId
            };

        Guid? capturedOrderId =
            null;

        NotificationsViewModel viewModel =
            CreateViewModel(
                new StubNotificationsApiClient(
                    (
                        requestedOrderId,
                        customerId,
                        correlationId,
                        offset,
                        limit,
                        _) =>
                    {
                        capturedOrderId =
                            requestedOrderId;

                        Assert.Null(
                            customerId);

                        Assert.Null(
                            correlationId);

                        Assert.Equal(
                            0,
                            offset);

                        Assert.Equal(
                            25,
                            limit);

                        return Task.FromResult(
                            new OperationalNotificationPageDto(
                                [notification],
                                offset,
                                limit,
                                false));
                    }));

        await viewModel.FocusOrderAsync(
            orderId,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            orderId,
            capturedOrderId);

        Assert.Equal(
            orderId.ToString("D"),
            viewModel.OrderIdText);

        Assert.Single(
            viewModel.Notifications);

        Assert.Same(
            notification,
            viewModel.SelectedNotification);
    }

    [Fact]
    public async Task
        FocusOrderAsyncDoesNotAutoSelectWhenOrderHasMultipleNotifications()
    {
        Guid orderId =
            Guid.NewGuid();

        OperationalNotificationDto first =
            CreateNotification() with
            {
                Id = Guid.NewGuid(),
                OrderId = orderId
            };

        OperationalNotificationDto second =
            CreateNotification() with
            {
                Id = Guid.NewGuid(),
                OrderId = orderId
            };

        NotificationsViewModel viewModel =
            CreateViewModel(
                new StubNotificationsApiClient(
                    (
                        _,
                        _,
                        _,
                        offset,
                        limit,
                        _) =>
                        Task.FromResult(
                            new OperationalNotificationPageDto(
                                [first, second],
                                offset,
                                limit,
                                false))));

        await viewModel.FocusOrderAsync(
            orderId,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            2,
            viewModel.Notifications.Count);

        Assert.Null(
            viewModel.SelectedNotification);
    }

    [Fact]
    public async Task
        ClearContextFocusRemovesContextualNotificationDataset()
    {
        Guid orderId =
            Guid.NewGuid();

        OperationalNotificationDto notification =
            CreateNotification() with
            {
                Id = Guid.NewGuid(),
                OrderId = orderId
            };

        NotificationsViewModel viewModel =
            CreateViewModel(
                new StubNotificationsApiClient(
                    (
                        _,
                        _,
                        _,
                        offset,
                        limit,
                        _) =>
                        Task.FromResult(
                            new OperationalNotificationPageDto(
                                [notification],
                                offset,
                                limit,
                                false))));

        await viewModel.FocusOrderAsync(
            orderId,
            TestContext.Current.CancellationToken);

        Assert.Single(
            viewModel.Notifications);

        viewModel.ClearContextFocus(
            orderId);

        Assert.Equal(
            string.Empty,
            viewModel.OrderIdText);

        Assert.Empty(
            viewModel.Notifications);

        Assert.Null(
            viewModel.SelectedNotification);

        Assert.False(
            viewModel.HasLoaded);

        Assert.Equal(
            "Notifications not loaded.",
            viewModel.StatusText);
    }
    private static NotificationsViewModel
        CreateViewModel(
            INotificationsApiClient? apiClient = null)
    {
        return new NotificationsViewModel(
            apiClient ?? new StubNotificationsApiClient(),
            NullLogger<NotificationsViewModel>.Instance);
    }

    private static OperationalNotificationDto
        CreateNotification()
    {
        return new OperationalNotificationDto(
            Guid.NewGuid(),
            "customer-123",
            Guid.NewGuid(),
            "OrderConfirmed",
            "Order confirmed",
            "Your order was confirmed.",
            false,
            DateTimeOffset.UtcNow,
            null,
            Guid.NewGuid(),
            Guid.NewGuid());
    }

    private sealed class StubNotificationsApiClient(
        Func<
            Guid?,
            string?,
            Guid?,
            int,
            int,
            CancellationToken,
            Task<OperationalNotificationPageDto>>?
        getNotifications = null)
        : INotificationsApiClient
    {
        public Task<OperationalNotificationPageDto>
            GetNotificationsAsync(
                Guid? orderId,
                string? customerId,
                Guid? correlationId,
                int offset,
                int limit,
                CancellationToken cancellationToken)
        {
            return getNotifications?.Invoke(
                orderId,
                customerId,
                correlationId,
                offset,
                limit,
                cancellationToken)
                ?? Task.FromResult(
                    new OperationalNotificationPageDto(
                        [],
                        offset,
                        limit,
                        false));
        }

        public Task<OperationalNotificationDto>
            GetNotificationAsync(
                Guid notificationId,
                CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "Notification detail API was not expected in this test.");
        }
    }
}