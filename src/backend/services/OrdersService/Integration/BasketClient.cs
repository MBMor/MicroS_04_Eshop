using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using OrdersService.Options;

namespace OrdersService.Integration;

public sealed class BasketClient(
    HttpClient httpClient,
    IOptions<OrdersOptions> ordersOptions) : IBasketClient
{
    private readonly OrdersOptions _ordersOptions =
        ordersOptions.Value;

    public async Task<BasketSnapshot> GetBasketAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Get,
            "api/v1/basket",
            customerId);

        using HttpResponseMessage response =
            await httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        BasketSnapshot? basket =
            await response.Content.ReadFromJsonAsync<BasketSnapshot>(
                cancellationToken);

        return basket
            ?? throw new InvalidOperationException(
                "Basket Service returned an empty response.");
    }

    public async Task ClearBasketAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Delete,
            "api/v1/basket",
            customerId);

        using HttpResponseMessage response =
            await httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        string customerId)
    {
        HttpRequestMessage request = new(method, path);

        request.Headers.TryAddWithoutValidation(
            _ordersOptions.DevelopmentCustomerHeaderName,
            customerId);

        return request;
    }
}
