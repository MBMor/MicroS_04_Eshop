using System.Net;
using System.Text;

using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.OperationalHealth;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Eshop.Operations.Desktop.Tests.Api.OperationalHealth;

public sealed class OperationalHealthApiClientTests
{
    [Fact]
    public async Task
        GetOperationalHealthAsyncUsesAuthenticatedGatewayAndMapsResponse()
    {
        const string json =
            """
            {
              "status": "Degraded",
              "checkedAtUtc": "2026-09-01T15:00:00Z",
              "services": [
                {
                  "service": "Orders",
                  "status": "Healthy",
                  "durationMilliseconds": 12,
                  "failureKind": null,
                  "httpStatusCode": 200
                },
                {
                  "service": "Payments",
                  "status": "Unavailable",
                  "durationMilliseconds": 2001,
                  "failureKind": "Timeout",
                  "httpStatusCode": null
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
                        "http://localhost:5080/api/v1/operations/health",
                        request.RequestUri?.ToString());

                    return new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            json,
                            Encoding.UTF8,
                            "application/json")
                    };
                });

        using var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri("http://localhost:5080/")
            };

        OperationalHealthApiClient client =
            new(
                new StubHttpClientFactory(
                    httpClient,
                    "ApiGatewayAuthenticated"),
                NullLogger<OperationalHealthApiClient>.Instance);

        OperationalHealthResponseDto response =
            await client.GetOperationalHealthAsync(
                CancellationToken.None);

        Assert.Equal(
            "Degraded",
            response.Status);
        Assert.Equal(
            2,
            response.Services.Count);
        OperationalServiceHealthDto payments =
            Assert.Single(
                response.Services,
                service => service.Service == "Payments");

        Assert.Equal(
            "Unavailable",
            payments.Status);
        Assert.Equal(
            "Timeout",
            payments.FailureKind);
        Assert.Null(
            payments.HttpStatusCode);

        OperationalServiceHealthDto orders =
            Assert.Single(
                response.Services,
                service => service.Service == "Orders");

        Assert.Equal(
            200,
            orders.HttpStatusCode);
        Assert.Null(
            orders.FailureKind);
    }

    [Fact]
    public async Task
        GetOperationalHealthAsyncMapsForbiddenToApiRequestException()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    new HttpResponseMessage(
                        HttpStatusCode.Forbidden)
                    {
                        Content = new StringContent(
                            "{\"status\":403,\"title\":\"Forbidden\"}",
                            Encoding.UTF8,
                            "application/problem+json")
                    });

        using var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri("http://localhost:5080/")
            };

        OperationalHealthApiClient client =
            new(
                new StubHttpClientFactory(
                    httpClient,
                    "ApiGatewayAuthenticated"),
                NullLogger<OperationalHealthApiClient>.Instance);

        ApiRequestException exception =
            await Assert.ThrowsAsync<ApiRequestException>(
                () =>
                    client.GetOperationalHealthAsync(
                        CancellationToken.None));

        Assert.Equal(
            HttpStatusCode.Forbidden,
            exception.StatusCode);
    }

    [Fact]
    public async Task
        GetOperationalHealthAsyncPropagatesTransportFailure()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    throw new HttpRequestException(
                        "Gateway unavailable."));

        using var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri("http://localhost:5080/")
            };

        OperationalHealthApiClient client =
            new(
                new StubHttpClientFactory(
                    httpClient,
                    "ApiGatewayAuthenticated"),
                NullLogger<OperationalHealthApiClient>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(
            () =>
                client.GetOperationalHealthAsync(
                    CancellationToken.None));
    }

    private sealed class StubHttpClientFactory(
        HttpClient httpClient,
        string expectedName)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(
            string name)
        {
            Assert.Equal(
                expectedName,
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
