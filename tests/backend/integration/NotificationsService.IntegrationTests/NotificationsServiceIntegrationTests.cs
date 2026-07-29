using System.Net;
using System.Net.Http.Json;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NotificationsService.Contracts;
using NotificationsService.Data;
using NotificationsService.Domain;
using NotificationsService.IntegrationTests.Infrastructure;
using Xunit;

namespace NotificationsService.IntegrationTests;

public sealed class NotificationsServiceIntegrationTests(
    NotificationsServiceFixture fixture)
    : IClassFixture<NotificationsServiceFixture>
{
    private const string NotificationsEndpoint =
        "/api/v1/notifications";

    private static readonly TimeSpan TimestampTolerance =
        TimeSpan.FromMicroseconds(1);

    private readonly HttpClient _client =
        fixture.Client;

    [Fact]
    public async Task
        HealthAnonymousRequestReturnsOk()
    {
        foreach (string endpoint in new[] { "/live", "/ready", "/health" })
        {
            using HttpResponseMessage response =
                await _client.GetAsync(
                    endpoint,
                    Xunit.TestContext.Current.CancellationToken);

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);
        }
    }

    [Fact]
    public async Task
        NotificationsAnonymousRequestReturnsUnauthorized()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                NotificationsEndpoint, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task
        NotificationsSupportUserReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                NotificationsEndpoint,
                CreateSubject("support"),
                EshopRoles.Support);

        using HttpResponseMessage response =
            await _client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task
        GetNotificationsNewCustomerReturnsEmptyCollection()
    {
        string customerId =
            CreateSubject("empty");

        using HttpResponseMessage response =
            await SendCustomerGetAsync(
                NotificationsEndpoint,
                customerId);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        NotificationResponse[] notifications =
            await ReadRequiredAsync<NotificationResponse[]>(
                response);

        Assert.Empty(notifications);
    }

    [Fact]
    public async Task
        GetNotificationsReturnsOnlyCurrentCustomerInDescendingOrder()
    {
        string customerId =
            CreateSubject("owner");

        string otherCustomerId =
            CreateSubject("other");

        DateTimeOffset baseline =
            DateTimeOffset.UtcNow.AddMinutes(-10);

        Notification olderNotification =
            await SeedNotificationAsync(
                customerId,
                baseline,
                type: NotificationType.OrderCreated);

        Notification newerNotification =
            await SeedNotificationAsync(
                customerId,
                baseline.AddMinutes(1),
                type: NotificationType.PaymentAuthorized);

        await SeedNotificationAsync(
            otherCustomerId,
            baseline.AddMinutes(2),
            type: NotificationType.OrderCancelled);

        using HttpResponseMessage response =
            await SendCustomerGetAsync(
                NotificationsEndpoint,
                customerId);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        NotificationResponse[] notifications =
            await ReadRequiredAsync<NotificationResponse[]>(
                response);

        Assert.Equal(
            2,
            notifications.Length);

        Assert.Equal(
            newerNotification.Id,
            notifications[0].Id);

        Assert.Equal(
            olderNotification.Id,
            notifications[1].Id);

        Assert.DoesNotContain(
            notifications,
            notification =>
                notification.Id != olderNotification.Id
                && notification.Id != newerNotification.Id);
    }

    [Fact]
    public async Task
        GetNotificationsUnreadOnlyReturnsOnlyUnreadNotifications()
    {
        string customerId =
            CreateSubject("unread");

        DateTimeOffset baseline =
            DateTimeOffset.UtcNow.AddMinutes(-10);

        Notification unreadNotification =
            await SeedNotificationAsync(
                customerId,
                baseline,
                isRead: false);

        Notification readNotification =
            await SeedNotificationAsync(
                customerId,
                baseline.AddMinutes(1),
                isRead: true);

        using HttpResponseMessage response =
            await SendCustomerGetAsync(
                $"{NotificationsEndpoint}?unreadOnly=true",
                customerId);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        NotificationResponse[] notifications =
            await ReadRequiredAsync<NotificationResponse[]>(
                response);

        NotificationResponse result =
            Assert.Single(notifications);

        Assert.Equal(
            unreadNotification.Id,
            result.Id);

        Assert.False(result.IsRead);

        Assert.DoesNotContain(
            notifications,
            notification =>
                notification.Id == readNotification.Id);
    }

    [Fact]
    public async Task
        GetNotificationsOrderFilterReturnsMatchingOrder()
    {
        string customerId =
            CreateSubject("order-filter");

        Guid matchingOrderId =
            Guid.NewGuid();

        Guid otherOrderId =
            Guid.NewGuid();

        Notification matchingNotification =
            await SeedNotificationAsync(
                customerId,
                DateTimeOffset.UtcNow.AddMinutes(-5),
                orderId: matchingOrderId);

        await SeedNotificationAsync(
            customerId,
            DateTimeOffset.UtcNow.AddMinutes(-4),
            orderId: otherOrderId);

        await SeedNotificationAsync(
            customerId,
            DateTimeOffset.UtcNow.AddMinutes(-3),
            orderId: null);

        using HttpResponseMessage response =
            await SendCustomerGetAsync(
                $"{NotificationsEndpoint}" +
                $"?orderId={matchingOrderId}",
                customerId);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        NotificationResponse[] notifications =
            await ReadRequiredAsync<NotificationResponse[]>(
                response);

        NotificationResponse result =
            Assert.Single(notifications);

        Assert.Equal(
            matchingNotification.Id,
            result.Id);

        Assert.Equal(
            matchingOrderId,
            result.OrderId);
    }

    [Fact]
    public async Task
        GetNotificationsLimitIsAppliedAfterDescendingOrdering()
    {
        string customerId =
            CreateSubject("limit");

        DateTimeOffset baseline =
            DateTimeOffset.UtcNow.AddMinutes(-10);

        await SeedNotificationAsync(
            customerId,
            baseline);

        Notification secondNewest =
            await SeedNotificationAsync(
                customerId,
                baseline.AddMinutes(1));

        Notification newest =
            await SeedNotificationAsync(
                customerId,
                baseline.AddMinutes(2));

        using HttpResponseMessage response =
            await SendCustomerGetAsync(
                $"{NotificationsEndpoint}?limit=2",
                customerId);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        NotificationResponse[] notifications =
            await ReadRequiredAsync<NotificationResponse[]>(
                response);

        Assert.Equal(
            2,
            notifications.Length);

        Assert.Equal(
            newest.Id,
            notifications[0].Id);

        Assert.Equal(
            secondNewest.Id,
            notifications[1].Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task
        GetNotificationsInvalidLimitReturnsBadRequest(
            int limit)
    {
        string customerId =
            CreateSubject("invalid-limit");

        using HttpResponseMessage response =
            await SendCustomerGetAsync(
                $"{NotificationsEndpoint}?limit={limit}",
                customerId);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ValidationProblemDetails problem =
            await ReadRequiredAsync<
                ValidationProblemDetails>(
                    response);

        Assert.NotEmpty(problem.Errors);
    }

    [Fact]
    public async Task
        GetNotificationsEmptyOrderIdReturnsBadRequest()
    {
        string customerId =
            CreateSubject("empty-order");

        using HttpResponseMessage response =
            await SendCustomerGetAsync(
                $"{NotificationsEndpoint}" +
                $"?orderId={Guid.Empty}",
                customerId);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ValidationProblemDetails problem =
            await ReadRequiredAsync<
                ValidationProblemDetails>(
                    response);

        Assert.Contains(
            "orderId",
            problem.Errors.Keys,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task
        GetUnreadCountReturnsOnlyCurrentCustomersUnreadCount()
    {
        string customerId =
            CreateSubject("unread-count");

        string otherCustomerId =
            CreateSubject("unread-count-other");

        DateTimeOffset baseline =
            DateTimeOffset.UtcNow.AddMinutes(-10);

        await SeedNotificationAsync(
            customerId,
            baseline,
            isRead: false);

        await SeedNotificationAsync(
            customerId,
            baseline.AddMinutes(1),
            isRead: false);

        await SeedNotificationAsync(
            customerId,
            baseline.AddMinutes(2),
            isRead: true);

        await SeedNotificationAsync(
            otherCustomerId,
            baseline.AddMinutes(3),
            isRead: false);

        using HttpResponseMessage response =
            await SendCustomerGetAsync(
                $"{NotificationsEndpoint}/unread-count",
                customerId);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        UnreadNotificationCountResponse result =
            await ReadRequiredAsync<
                UnreadNotificationCountResponse>(
                    response);

        Assert.Equal(
            2,
            result.Count);
    }

    [Fact]
    public async Task
        GetNotificationByIdOwnerReturnsPersistedNotification()
    {
        string customerId =
            CreateSubject("detail-owner");

        Guid orderId =
            Guid.NewGuid();

        DateTimeOffset createdAtUtc =
            DateTimeOffset.UtcNow.AddMinutes(-5);

        Notification notification =
            await SeedNotificationAsync(
                customerId,
                createdAtUtc,
                orderId,
                NotificationType.PaymentFailed);

        using HttpResponseMessage response =
            await SendCustomerGetAsync(
                $"{NotificationsEndpoint}/{notification.Id}",
                customerId);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        NotificationResponse result =
            await ReadRequiredAsync<NotificationResponse>(
                response);

        Assert.Equal(
            notification.Id,
            result.Id);

        Assert.Equal(
            orderId,
            result.OrderId);

        Assert.Equal(
            NotificationType.PaymentFailed.ToString(),
            result.Type);

        Assert.Equal(
            notification.Title,
            result.Title);

        Assert.Equal(
            notification.Message,
            result.Message);

        Assert.False(result.IsRead);
        Assert.Null(result.ReadAtUtc);

        AssertTimestampEqual(
            notification.CreatedAtUtc,
            result.CreatedAtUtc);
    }

    [Fact]
    public async Task
        GetNotificationByIdOtherCustomerReturnsNotFound()
    {
        string ownerId =
            CreateSubject("detail-owner");

        string otherCustomerId =
            CreateSubject("detail-other");

        Notification notification =
            await SeedNotificationAsync(
                ownerId,
                DateTimeOffset.UtcNow.AddMinutes(-5));

        using HttpResponseMessage response =
            await SendCustomerGetAsync(
                $"{NotificationsEndpoint}/{notification.Id}",
                otherCustomerId);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        ProblemDetails problem =
            await ReadRequiredAsync<ProblemDetails>(
                response);

        Assert.Equal(
            "Notification was not found.",
            problem.Title);
    }

    private async Task<Notification>
        SeedNotificationAsync(
            string customerId,
            DateTimeOffset createdAtUtc,
            Guid? orderId = null,
            NotificationType type =
                NotificationType.OrderCreated,
            bool isRead = false)
    {
        Notification notification =
            Notification.Create(
                Guid.NewGuid(),
                customerId,
                orderId,
                type,
                title: $"{type} notification",
                message:
                    $"Integration notification for {type}.",
                createdAtUtc,
                sourceEventId: Guid.NewGuid(),
                correlationId: Guid.NewGuid());

        if (isRead)
        {
            notification.MarkAsRead(
                createdAtUtc.AddSeconds(1));
        }

        await using AsyncServiceScope scope =
            fixture.Factory.Services.CreateAsyncScope();

        NotificationsDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<NotificationsDbContext>();

        dbContext.Notifications.Add(notification);

        await dbContext.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        return notification;
    }

    private async Task<HttpResponseMessage>
        SendCustomerGetAsync(
            string path,
            string customerId)
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                path,
                customerId,
                EshopRoles.Customer);

        return await _client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);
    }

    private static HttpRequestMessage
        CreateAuthenticatedRequest(
            HttpMethod method,
            string path,
            string subject,
            params string[] roles)
    {
        HttpRequestMessage request =
            new(method, path);

        request.Headers.Add(
            TestAuthenticationHandler.SubjectHeaderName,
            subject);

        request.Headers.Add(
            TestAuthenticationHandler.RolesHeaderName,
            string.Join(',', roles));

        return request;
    }

    private static async Task<T>
        ReadRequiredAsync<T>(
            HttpResponseMessage response)
    {
        T? value =
            await response.Content
                .ReadFromJsonAsync<T>(Xunit.TestContext.Current.CancellationToken);

        return Assert.IsType<T>(value);
    }

    private static void AssertTimestampEqual(
        DateTimeOffset expected,
        DateTimeOffset actual)
    {
        TimeSpan difference =
            (expected - actual).Duration();

        Assert.InRange(
            difference,
            TimeSpan.Zero,
            TimestampTolerance);
    }

    private static string CreateSubject(
        string scenario)
    {
        return $"{scenario}-{Guid.NewGuid():N}";
    }
}
