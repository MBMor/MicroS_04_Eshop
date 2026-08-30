using System.Net;
using System.Text;

using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Orders;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Eshop.Operations.Desktop.Tests.Api.Orders;

public sealed class OrdersApiClientTests
{
    [Fact]
    public async Task GetOrdersAsyncMapsBoundedOperationalPage()
    {
        const string json =
            """
            {
              "items": [
                {
                  "id": "b263c90c-e541-4274-90ce-e55992ea6c63",
                  "customerId": "customer-123",
                  "customerEmail": "customer@example.com",
                  "status": "Confirmed",
                  "totalAmount": 1499.50,
                  "currency": "CZK",
                  "itemCount": 3,
                  "createdAtUtc": "2026-08-30T10:00:00+00:00",
                  "updatedAtUtc": "2026-08-30T10:02:00+00:00"
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
                        "http://localhost:5080/api/v1/operations/orders?offset=25&limit=25",
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
            new OrdersApiClient(
                new StubHttpClientFactory(
                    httpClient),
                NullLogger<OrdersApiClient>.Instance);

        OperationalOrderPageDto page =
            await client.GetOrdersAsync(
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

        OperationalOrderSummaryDto order =
            Assert.Single(
                page.Items);

        Assert.Equal(
            "customer-123",
            order.CustomerId);

        Assert.Equal(
            "customer@example.com",
            order.CustomerEmail);

        Assert.Equal(
            "Confirmed",
            order.Status);

        Assert.Equal(
            1499.50m,
            order.TotalAmount);

        Assert.Equal(
            "CZK",
            order.Currency);

        Assert.Equal(
            3,
            order.ItemCount);
    }

    [Fact]
    public async Task GetOrderAsyncMapsItemsAndStatusHistory()
    {
        Guid orderId =
            Guid.Parse(
                "b263c90c-e541-4274-90ce-e55992ea6c63");

        const string json =
            """
            {
              "id": "b263c90c-e541-4274-90ce-e55992ea6c63",
              "customerId": "customer-123",
              "customerEmail": "customer@example.com",
              "status": "Confirmed",
              "totalAmount": 1499.50,
              "currency": "CZK",
              "paymentMethod": "test-success",
              "createdAtUtc": "2026-08-30T10:00:00+00:00",
              "updatedAtUtc": "2026-08-30T10:02:00+00:00",
              "items": [
                {
                  "id": "c75c18b6-5b4a-4558-9965-71051980c447",
                  "productId": "84f22108-8ce4-42d3-adbc-f993464751ca",
                  "productName": "Mechanical Keyboard",
                  "unitPrice": 499.50,
                  "currency": "CZK",
                  "quantity": 3,
                  "lineTotal": 1498.50
                }
              ],
              "statusHistory": [
                {
                  "fromStatus": null,
                  "toStatus": "Pending",
                  "reason": "Order created.",
                  "changedAtUtc": "2026-08-30T10:00:00+00:00"
                },
                {
                  "fromStatus": "Pending",
                  "toStatus": "Confirmed",
                  "reason": "Order confirmed.",
                  "changedAtUtc": "2026-08-30T10:02:00+00:00"
                }
              ]
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
                        $"http://localhost:5080/api/v1/operations/orders/{orderId:D}",
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
            new OrdersApiClient(
                new StubHttpClientFactory(
                    httpClient),
                NullLogger<OrdersApiClient>.Instance);

        OperationalOrderDetailDto order =
            await client.GetOrderAsync(
                orderId,
                CancellationToken.None);

        Assert.Equal(
            orderId,
            order.Id);

        Assert.Equal(
            "customer-123",
            order.CustomerId);

        Assert.Equal(
            "customer@example.com",
            order.CustomerEmail);

        Assert.Equal(
            "test-success",
            order.PaymentMethod);

        OperationalOrderItemDto item =
            Assert.Single(
                order.Items);

        Assert.Equal(
            "Mechanical Keyboard",
            item.ProductName);

        Assert.Equal(
            3,
            item.Quantity);

        Assert.Equal(
            1498.50m,
            item.LineTotal);

        Assert.Equal(
            2,
            order.StatusHistory.Count);

        Assert.Equal(
            "Pending",
            order.StatusHistory[0].ToStatus);

        Assert.Equal(
            "Confirmed",
            order.StatusHistory[1].ToStatus);
    }

    [Fact]
    public async Task GetOrdersAsyncRejectsInvalidLimitBeforeSendingRequest()
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
            new OrdersApiClient(
                new StubHttpClientFactory(
                    httpClient),
                NullLogger<OrdersApiClient>.Instance);

        await Assert.ThrowsAsync<
            ArgumentOutOfRangeException>(
                () =>
                    client.GetOrdersAsync(
                        offset: 0,
                        limit: 101,
                        CancellationToken.None));
    }

    [Fact]
    public async Task GetOrderAsyncForbiddenResponseThrowsApiRequestException()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    new HttpResponseMessage(
                        HttpStatusCode.Forbidden)
                    {
                        Content =
                            new StringContent(
                                """
                                {
                                  "title": "Forbidden",
                                  "status": 403
                                }
                                """,
                                Encoding.UTF8,
                                "application/problem+json")
                    });

        using var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri(
                        "http://localhost:5080/")
            };

        var client =
            new OrdersApiClient(
                new StubHttpClientFactory(
                    httpClient),
                NullLogger<OrdersApiClient>.Instance);

        ApiRequestException exception =
            await Assert.ThrowsAsync<
                ApiRequestException>(
                () =>
                    client.GetOrderAsync(
                        Guid.NewGuid(),
                        CancellationToken.None));

        Assert.Equal(
            HttpStatusCode.Forbidden,
            exception.StatusCode);
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
