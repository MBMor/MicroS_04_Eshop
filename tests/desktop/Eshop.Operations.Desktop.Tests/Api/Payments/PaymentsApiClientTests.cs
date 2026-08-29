using System.Net;
using System.Text;
using Eshop.Operations.Desktop.Api.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Eshop.Operations.Desktop.Tests.Api.Payments;

public sealed class PaymentsApiClientTests
{
    [Fact]
    public async Task GetPaymentsAsyncMapsSuccessfulResponse()
    {
        const string json =
            """
            [
              {
                "id": "fa2d34dd-992e-449d-a92e-a30c7dfb136e",
                "orderId": "7289c76e-3f91-4431-a783-d89a07668d65",
                "customerId": "customer-123",
                "amount": 1499.50,
                "currency": "CZK",
                "paymentMethod": "test-success",
                "status": "Succeeded",
                "failureReason": null,
                "createdAtUtc": "2026-08-29T10:00:00+00:00",
                "processedAtUtc": "2026-08-29T10:00:02+00:00"
              }
            ]
            """;

        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal(
                    "http://localhost:5080/api/v1/payments",
                    request.RequestUri?.ToString());

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        json,
                        Encoding.UTF8,
                        "application/json")
                };
            });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5080/")
        };

        var client = new PaymentsApiClient(
            new StubHttpClientFactory(httpClient),
            NullLogger<PaymentsApiClient>.Instance);

        PaymentDto payment = Assert.Single(
            await client.GetPaymentsAsync(CancellationToken.None));

        Assert.Equal("customer-123", payment.CustomerId);
        Assert.Equal(1499.50m, payment.Amount);
        Assert.Equal("CZK", payment.Currency);
        Assert.Equal("test-success", payment.PaymentMethod);
        Assert.Equal("Succeeded", payment.Status);
    }

    private sealed class StubHttpClientFactory(HttpClient httpClient)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("ApiGatewayAuthenticated", name);
            return httpClient;
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(send(request, cancellationToken));
    }
}
