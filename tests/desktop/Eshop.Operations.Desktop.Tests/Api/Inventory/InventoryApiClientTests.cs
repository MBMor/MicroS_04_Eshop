using System.Net;
using System.Text;
using Eshop.Operations.Desktop.Api.Inventory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Eshop.Operations.Desktop.Tests.Api.Inventory;

public sealed class InventoryApiClientTests
{
    [Fact]
    public async Task GetInventoryItemsAsyncMapsSuccessfulResponse()
    {
        const string json =
            """
            [
              {
                "id": "56bc4387-e277-4ad5-bd64-1724575a8d98",
                "productId": "714db4a2-af39-4bfd-ae09-a79584af31ef",
                "sku": "KEY-001",
                "onHandQuantity": 20,
                "reservedQuantity": 5,
                "availableQuantity": 15,
                "isActive": true,
                "createdAtUtc": "2026-08-01T10:00:00+00:00",
                "updatedAtUtc": null
              }
            ]
            """;

        var handler =
            new StubHttpMessageHandler(
                (request, _) =>
                {
                    Assert.Equal(
                        HttpMethod.Get,
                        request.Method);

                    Assert.Equal(
                        "http://localhost:5080/api/v1/inventory-items",
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
                        "http://localhost:5080/",
                        UriKind.Absolute)
            };

        var client =
            new InventoryApiClient(
                new StubHttpClientFactory(
                    httpClient),
                NullLogger<InventoryApiClient>.Instance);

        IReadOnlyList<InventoryItemDto> items =
            await client.GetInventoryItemsAsync(
                false,
                CancellationToken.None);

        InventoryItemDto item =
            Assert.Single(items);

        Assert.Equal(
            "KEY-001",
            item.Sku);

        Assert.Equal(
            20,
            item.OnHandQuantity);

        Assert.Equal(
            5,
            item.ReservedQuantity);

        Assert.Equal(
            15,
            item.AvailableQuantity);
    }

    private sealed class StubHttpClientFactory(
        HttpClient httpClient)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(
            string name)
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
}
