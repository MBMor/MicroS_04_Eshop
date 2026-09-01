using System.Net;
using System.Text;

using Eshop.Operations.Desktop.Api.Notifications;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Eshop.Operations.Desktop.Tests.Api.Notifications;

public sealed class NotificationsApiClientTests
{
    [Fact]
    public async Task
        GetNotificationsAsyncMapsFilteredBoundedPage()
    {
        Guid orderId =
            Guid.Parse(
                "11111111-1111-1111-1111-111111111111");

        Guid correlationId =
            Guid.Parse(
                "22222222-2222-2222-2222-222222222222");

        Guid notificationId =
            Guid.Parse(
                "33333333-3333-3333-3333-333333333333");

        const string json =
            """
            {
              "items": [
                {
                  "id": "33333333-3333-3333-3333-333333333333",
                  "customerId": "customer-123",
                  "orderId": "11111111-1111-1111-1111-111111111111",
                  "type": "OrderConfirmed",
                  "title": "Order confirmed",
                  "message": "Your order was confirmed.",
                  "isRead": false,
                  "createdAtUtc": "2026-09-01T08:00:00+00:00",
                  "readAtUtc": null,
                  "sourceEventId": "44444444-4444-4444-4444-444444444444",
                  "correlationId": "22222222-2222-2222-2222-222222222222"
                }
              ],
              "offset": 25,
              "limit": 25,
              "hasMore": true
            }
            """;

        var handler =
            new StubHttpMessageHandler(
                (request, _) =>
                {
                    Assert.Equal(
                        HttpMethod.Get,
                        request.Method);

                    Assert.Equal(
                        $"http://localhost:5080/api/v1/operations/notifications" +
                        $"?orderId={orderId:D}" +
                        "&customerId=customer-123" +
                        $"&correlationId={correlationId:D}" +
                        "&offset=25&limit=25",
                        request.RequestUri?.ToString());

                    return new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new StringContent(
                                json,
                                Encoding.UTF8,
                                "application/json")
                    };
                });

        using var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri(
                        "http://localhost:5080/")
            };

        var client =
            new NotificationsApiClient(
                new StubHttpClientFactory(
                    httpClient),
                NullLogger<NotificationsApiClient>.Instance);

        OperationalNotificationPageDto page =
            await client.GetNotificationsAsync(
                orderId,
                " customer-123 ",
                correlationId,
                offset: 25,
                limit: 25,
                CancellationToken.None);

        Assert.Equal(
            25,
            page.Offset);

        Assert.Equal(
            25,
            page.Limit);

        Assert.True(
            page.HasMore);

        OperationalNotificationDto notification =
            Assert.Single(
                page.Items);

        Assert.Equal(
            notificationId,
            notification.Id);

        Assert.Equal(
            orderId,
            notification.OrderId);

        Assert.Equal(
            correlationId,
            notification.CorrelationId);

        Assert.Equal(
            "OrderConfirmed",
            notification.Type);

        Assert.False(
            notification.IsRead);
    }

    [Fact]
    public async Task
        GetNotificationsAsyncWithoutFiltersSendsOnlyPaging()
    {
        var handler =
            new StubHttpMessageHandler(
                (request, _) =>
                {
                    Assert.Equal(
                        "http://localhost:5080/api/v1/operations/notifications?offset=0&limit=50",
                        request.RequestUri?.ToString());

                    return new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new StringContent(
                                """
                                {
                                  "items": [],
                                  "offset": 0,
                                  "limit": 50,
                                  "hasMore": false
                                }
                                """,
                                Encoding.UTF8,
                                "application/json")
                    };
                });

        using var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri(
                        "http://localhost:5080/")
            };

        var client =
            new NotificationsApiClient(
                new StubHttpClientFactory(
                    httpClient),
                NullLogger<NotificationsApiClient>.Instance);

        OperationalNotificationPageDto page =
            await client.GetNotificationsAsync(
                orderId: null,
                customerId: null,
                correlationId: null,
                offset: 0,
                limit: 50,
                CancellationToken.None);

        Assert.Empty(
            page.Items);

        Assert.False(
            page.HasMore);
    }

    [Fact]
    public async Task
        GetNotificationAsyncMapsOperationalDetail()
    {
        Guid notificationId =
            Guid.Parse(
                "33333333-3333-3333-3333-333333333333");

        const string json =
            """
            {
              "id": "33333333-3333-3333-3333-333333333333",
              "customerId": "customer-123",
              "orderId": "11111111-1111-1111-1111-111111111111",
              "type": "OrderConfirmed",
              "title": "Order confirmed",
              "message": "Your order was confirmed.",
              "isRead": false,
              "createdAtUtc": "2026-09-01T08:00:00+00:00",
              "readAtUtc": null,
              "sourceEventId": "44444444-4444-4444-4444-444444444444",
              "correlationId": "22222222-2222-2222-2222-222222222222"
            }
            """;

        var handler =
            new StubHttpMessageHandler(
                (request, _) =>
                {
                    Assert.Equal(
                        $"http://localhost:5080/api/v1/operations/notifications/{notificationId:D}",
                        request.RequestUri?.ToString());

                    return new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new StringContent(
                                json,
                                Encoding.UTF8,
                                "application/json")
                    };
                });

        using var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri(
                        "http://localhost:5080/")
            };

        var client =
            new NotificationsApiClient(
                new StubHttpClientFactory(
                    httpClient),
                NullLogger<NotificationsApiClient>.Instance);

        OperationalNotificationDto notification =
            await client.GetNotificationAsync(
                notificationId,
                CancellationToken.None);

        Assert.Equal(
            notificationId,
            notification.Id);

        Assert.Equal(
            "customer-123",
            notification.CustomerId);

        Assert.Equal(
            "OrderConfirmed",
            notification.Type);

        Assert.NotNull(
            notification.SourceEventId);

        Assert.NotNull(
            notification.CorrelationId);
    }

    [Fact]
    public async Task
        GetNotificationsAsyncRejectsInvalidLimitBeforeSendingRequest()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    throw new InvalidOperationException(
                        "HTTP request was not expected."));

        using var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri(
                        "http://localhost:5080/")
            };

        var client =
            new NotificationsApiClient(
                new StubHttpClientFactory(
                    httpClient),
                NullLogger<NotificationsApiClient>.Instance);

        await Assert.ThrowsAsync<
            ArgumentOutOfRangeException>(
                () =>
                    client.GetNotificationsAsync(
                        null,
                        null,
                        null,
                        0,
                        101,
                        CancellationToken.None));
    }

    private sealed class StubHttpClientFactory(
        HttpClient httpClient)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(
            string name)
        {
            Assert.Equal(
                "ApiGatewayAuthenticated",
                name);

            return httpClient;
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<
            HttpRequestMessage,
            CancellationToken,
            HttpResponseMessage> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            return Task.FromResult(
                send(
                    request,
                    cancellationToken));
        }
    }
}