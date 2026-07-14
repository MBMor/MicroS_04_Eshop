using System.Net;
using System.Net.Http.Json;

namespace BasketService.Integration;

public sealed class CatalogClient(HttpClient httpClient) : ICatalogClient
{
    public async Task<CatalogProductSnapshot?> GetProductAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(
            $"api/v1/products/{productId}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        CatalogProductResponse? product =
            await response.Content.ReadFromJsonAsync<CatalogProductResponse>(
                cancellationToken);

        if (product is null)
        {
            throw new InvalidOperationException(
                "Catalog Service returned an empty product response.");
        }

        return new CatalogProductSnapshot(
            product.Id,
            product.Name,
            product.PriceAmount,
            product.Currency,
            product.IsActive);
    }

    private sealed record CatalogProductResponse(
        Guid Id,
        string Name,
        string Sku,
        string Description,
        string Category,
        decimal PriceAmount,
        string Currency,
        bool IsActive,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);
}
