using System.Net;
using System.Text;
using System.Text.Json;
using Eshop.Operations.Desktop.Api;
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
                "updatedAtUtc": null,
                "version": 42
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

        Assert.Equal(
            42u,
            item.Version);
    }

    [Fact]
    public async Task
        GetStockAdjustmentHistoryAsyncMapsBoundedPage()
    {
        Guid inventoryItemId = Guid.Parse("56bc4387-e277-4ad5-bd64-1724575a8d98");
        const string json =
            """
            {
              "items": [
                {
                  "operationId": "69f9379e-2f2c-42ae-b2ac-14a06868e126",
                  "inventoryItemId": "56bc4387-e277-4ad5-bd64-1724575a8d98",
                  "productId": "714db4a2-af39-4bfd-ae09-a79584af31ef",
                  "sku": "KEY-001",
                  "quantityDelta": -5,
                  "expectedVersion": 42,
                  "reason": "Physical count correction",
                  "actorSubject": "admin-123",
                  "actorUsername": "anna.admin",
                  "traceId": "trace-123",
                  "outcome": "Success",
                  "error": null,
                  "onHandBefore": 20,
                  "reservedBefore": 5,
                  "availableBefore": 15,
                  "onHandAfter": 15,
                  "reservedAfter": 5,
                  "availableAfter": 10,
                  "resultVersion": 43,
                  "occurredAtUtc": "2026-08-30T10:00:00+00:00"
                }
              ],
              "offset": 0,
              "limit": 25,
              "hasMore": true
            }
            """;

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                $"http://localhost:5080/api/v1/inventory-items/{inventoryItemId}/stock-adjustments?offset=0&limit=25",
                request.RequestUri?.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5080/", UriKind.Absolute)
        };
        var client = new InventoryApiClient(
            new StubHttpClientFactory(httpClient),
            NullLogger<InventoryApiClient>.Instance);

        InventoryStockAdjustmentHistoryPageDto page =
            await client.GetStockAdjustmentHistoryAsync(
                inventoryItemId, 0, 25, CancellationToken.None);

        Assert.Equal(0, page.Offset);
        Assert.Equal(25, page.Limit);
        Assert.True(page.HasMore);
        InventoryStockAdjustmentHistoryItemDto item = Assert.Single(page.Items);
        Assert.Equal(-5, item.QuantityDelta);
        Assert.Equal("anna.admin", item.ActorUsername);
        Assert.Equal("Physical count correction", item.Reason);
        Assert.Equal("Success", item.Outcome);
        Assert.Equal(20, item.OnHandBefore);
        Assert.Equal(15, item.OnHandAfter);
        Assert.Equal(43, item.ResultVersion);
    }

    [Fact]
    public async Task
        AdjustStockAsyncSendsSafetyContractAndMapsSuccessfulResponse()
    {
        Guid itemId = Guid.Parse("56bc4387-e277-4ad5-bd64-1724575a8d98");
        Guid idempotencyKey = Guid.Parse("69f9379e-2f2c-42ae-b2ac-14a06868e126");
        const string responseJson =
            """
            {
              "id": "56bc4387-e277-4ad5-bd64-1724575a8d98",
              "productId": "714db4a2-af39-4bfd-ae09-a79584af31ef",
              "sku": "KEY-001",
              "onHandQuantity": 15,
              "reservedQuantity": 5,
              "availableQuantity": 10,
              "isActive": true,
              "createdAtUtc": "2026-08-01T10:00:00+00:00",
              "updatedAtUtc": "2026-08-30T10:00:00+00:00",
              "version": 43
            }
            """;

        var handler = new AsyncStubHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(
                    $"http://localhost:5080/api/v1/inventory-items/{itemId}/stock-adjustments",
                    request.RequestUri?.ToString());
                Assert.True(request.Headers.TryGetValues(
                    "Idempotency-Key",
                    out IEnumerable<string>? values));
                Assert.Equal(idempotencyKey.ToString("D"), Assert.Single(values));

                string json = await request.Content!.ReadAsStringAsync(cancellationToken);
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                Assert.Equal(-5, root.GetProperty("quantityDelta").GetInt32());
                Assert.Equal(42u, root.GetProperty("expectedVersion").GetUInt32());
                Assert.Equal(
                    "Physical count correction",
                    root.GetProperty("reason").GetString());

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        responseJson,
                        Encoding.UTF8,
                        "application/json")
                };
            });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5080/", UriKind.Absolute)
        };
        var client = new InventoryApiClient(
            new StubHttpClientFactory(httpClient),
            NullLogger<InventoryApiClient>.Instance);
        var request = new InventoryStockAdjustmentRequest(
            itemId,
            -5,
            42,
            "Physical count correction",
            idempotencyKey);

        InventoryStockAdjustmentResult result =
            await client.AdjustStockAsync(request, CancellationToken.None);

        Assert.False(result.IsReplay);
        Assert.Equal(idempotencyKey, result.IdempotencyKey);
        Assert.Equal(15, result.Item.OnHandQuantity);
        Assert.Equal(43u, result.Item.Version);
    }

    [Fact]
    public async Task AdjustStockAsyncMapsIdempotentReplayResponse()
    {
        const string responseJson =
            """
            {
              "id": "56bc4387-e277-4ad5-bd64-1724575a8d98",
              "productId": "714db4a2-af39-4bfd-ae09-a79584af31ef",
              "sku": "KEY-001",
              "onHandQuantity": 15,
              "reservedQuantity": 5,
              "availableQuantity": 10,
              "isActive": true,
              "createdAtUtc": "2026-08-01T10:00:00+00:00",
              "updatedAtUtc": "2026-08-30T10:00:00+00:00",
              "version": 43
            }
            """;
        var handler = new StubHttpMessageHandler(
            (_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        responseJson,
                        Encoding.UTF8,
                        "application/json")
                };
                response.Headers.Add("Idempotent-Replay", "true");
                return response;
            });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5080/", UriKind.Absolute)
        };
        var client = new InventoryApiClient(
            new StubHttpClientFactory(httpClient),
            NullLogger<InventoryApiClient>.Instance);
        InventoryStockAdjustmentResult result =
            await client.AdjustStockAsync(
                new InventoryStockAdjustmentRequest(
                    Guid.NewGuid(),
                    1,
                    42,
                    "Physical count correction",
                    Guid.NewGuid()),
                CancellationToken.None);

        Assert.True(result.IsReplay);
    }

    [Fact]
    public async Task
        AdjustStockAsyncTransportFailureReportsUnknownOutcomeWithSameIdempotencyKey()
    {
        Guid idempotencyKey = Guid.NewGuid();
        var handler = new StubHttpMessageHandler(
            (_, _) => throw new HttpRequestException("Connection reset."));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5080/", UriKind.Absolute)
        };
        var client = new InventoryApiClient(
            new StubHttpClientFactory(httpClient),
            NullLogger<InventoryApiClient>.Instance);
        InventoryStockAdjustmentRequest request = new(
            Guid.NewGuid(),
            2,
            42,
            "Physical count correction",
            idempotencyKey);

        InventoryStockAdjustmentOutcomeUnknownException exception =
            await Assert.ThrowsAsync<InventoryStockAdjustmentOutcomeUnknownException>(
                () => client.AdjustStockAsync(request, CancellationToken.None));

        Assert.Equal(idempotencyKey, exception.IdempotencyKey);
    }

    [Fact]
    public async Task AdjustStockAsyncConflictRemainsDeterministicApiFailure()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    """
                    {
                      "title": "Inventory conflict.",
                      "detail": "Inventory item has changed since it was loaded."
                    }
                    """,
                    Encoding.UTF8,
                    "application/problem+json")
            });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5080/", UriKind.Absolute)
        };
        var client = new InventoryApiClient(
            new StubHttpClientFactory(httpClient),
            NullLogger<InventoryApiClient>.Instance);

        Task Act() => client.AdjustStockAsync(
            new InventoryStockAdjustmentRequest(
                Guid.NewGuid(),
                2,
                42,
                "Physical count correction",
                Guid.NewGuid()),
            CancellationToken.None);

        ApiRequestException exception =
            await Assert.ThrowsAsync<ApiRequestException>(Act);
        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
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

    private sealed class AsyncStubHttpMessageHandler(
        Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return send(
                request,
                cancellationToken);
        }
    }
}
