using System.Net;
using System.Text;
using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Catalog;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Eshop.Operations.Desktop.Tests.Api.Catalog;

public sealed class CatalogApiClientTests
{
    [Fact]
    public async Task GetProductsAsyncMapsSuccessfulResponse()
    {
        const string json =
            """
            [
              {
                "id": "88c699b1-7417-4e41-a909-a666e1866c38",
                "name": "Mechanical Keyboard",
                "sku": "KEY-001",
                "description": "Mechanical keyboard",
                "category": "Peripherals",
                "priceAmount": 129.90,
                "currency": "EUR",
                "isActive": true,
                "createdAtUtc": "2026-08-01T10:00:00+00:00",
                "updatedAtUtc": null
              }
            ]
            """;

        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                Assert.Equal(
                    HttpMethod.Get,
                    request.Method);

                Assert.Equal(
                    "http://localhost:5080/api/v1/products",
                    request.RequestUri?.ToString());

                return CreateJsonResponse(
                    HttpStatusCode.OK,
                    json);
            });

        CatalogApiClient client = CreateClient(handler);

        IReadOnlyList<CatalogProductDto> products =
            await client.GetProductsAsync(
                includeInactive: false,
                CancellationToken.None);

        CatalogProductDto product = Assert.Single(products);

        Assert.Equal(
            Guid.Parse(
                "88c699b1-7417-4e41-a909-a666e1866c38"),
            product.Id);

        Assert.Equal(
            "Mechanical Keyboard",
            product.Name);

        Assert.Equal(
            "KEY-001",
            product.Sku);

        Assert.Equal(
            129.90m,
            product.PriceAmount);

        Assert.Equal(
            "EUR",
            product.Currency);

        Assert.True(product.IsActive);
    }

    [Fact]
    public async Task GetProductsAsyncIncludesInactiveQueryWhenRequested()
    {
        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                Assert.Equal(
                    "http://localhost:5080/api/v1/products?includeInactive=true",
                    request.RequestUri?.ToString());

                return CreateJsonResponse(
                    HttpStatusCode.OK,
                    "[]");
            });

        CatalogApiClient client = CreateClient(handler);

        IReadOnlyList<CatalogProductDto> products =
            await client.GetProductsAsync(
                includeInactive: true,
                CancellationToken.None);

        Assert.Empty(products);
    }

    [Fact]
    public async Task GetProductsAsyncPropagatesCancellation()
    {
        using var cancellationTokenSource =
            new CancellationTokenSource();

        var handler = new CancellationObservingHandler();

        CatalogApiClient client = CreateClient(handler);

        Task<IReadOnlyList<CatalogProductDto>> requestTask =
            client.GetProductsAsync(
                includeInactive: false,
                cancellationTokenSource.Token);

        await handler.RequestStarted;

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await requestTask);
    }

    [Fact]
    public async Task GetProductsAsyncPreservesProblemDetailsForHttpFailure()
    {
        const string json =
            """
            {
              "status": 503,
              "title": "Service unavailable",
              "detail": "Catalog dependency is unavailable.",
              "errorCode": "dependency_unavailable",
              "traceId": "trace-123",
              "requestId": "request-456"
            }
            """;

        var handler = new StubHttpMessageHandler(
            (_, _) =>
                CreateJsonResponse(
                    HttpStatusCode.ServiceUnavailable,
                    json,
                    "application/problem+json"));

        CatalogApiClient client = CreateClient(handler);

        ApiRequestException exception =
            await Assert.ThrowsAsync<ApiRequestException>(
                () => client.GetProductsAsync(
                    includeInactive: false,
                    CancellationToken.None));

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            exception.StatusCode);

        Assert.NotNull(exception.ProblemDetails);

        Assert.Equal(
            "dependency_unavailable",
            exception.ProblemDetails.ErrorCode);

        Assert.Equal(
            "trace-123",
            exception.ProblemDetails.TraceId);

        Assert.Equal(
            "request-456",
            exception.ProblemDetails.RequestId);
    }

    [Fact]
    public async Task GetProductsAsyncDoesNotRetryFailedRequest()
    {
        var requestCount = 0;

        var handler = new StubHttpMessageHandler(
            (_, _) =>
            {
                requestCount++;

                return CreateJsonResponse(
                    HttpStatusCode.ServiceUnavailable,
                    "{}",
                    "application/problem+json");
            });

        CatalogApiClient client = CreateClient(handler);

        await Assert.ThrowsAsync<ApiRequestException>(
            () => client.GetProductsAsync(
                includeInactive: false,
                CancellationToken.None));

        Assert.Equal(1, requestCount);
    }

    private static CatalogApiClient CreateClient(
        HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress =
                new Uri(
                    "http://localhost:5080/",
                    UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(15)
        };

        var factory =
            new StubHttpClientFactory(httpClient);

        return new CatalogApiClient(
            factory,
            NullLogger<CatalogApiClient>.Instance);
    }

    private static HttpResponseMessage CreateJsonResponse(
        HttpStatusCode statusCode,
        string json,
        string mediaType = "application/json")
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                mediaType)
        };
    }

    private sealed class StubHttpClientFactory(
        HttpClient httpClient)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
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
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                send(
                    request,
                    cancellationToken));
        }
    }

    private sealed class CancellationObservingHandler
    : HttpMessageHandler
    {
        private readonly TaskCompletionSource _requestStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RequestStarted => _requestStarted.Task;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _requestStarted.SetResult();

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            throw new InvalidOperationException(
                "The request should have been cancelled.");
        }
    }
}
