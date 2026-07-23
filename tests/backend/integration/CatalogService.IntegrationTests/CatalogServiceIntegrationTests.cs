using System.Net;
using System.Net.Http.Json;

using CatalogService.Contracts;
using CatalogService.IntegrationTests.Infrastructure;
using Xunit;

namespace CatalogService.IntegrationTests;

public sealed class CatalogServiceIntegrationTests(
    CatalogServiceFixture fixture)
    : IClassFixture<CatalogServiceFixture>
{
    private const string ProductsEndpoint =
        "/api/v1/products";

    private static readonly TimeSpan TimestampTolerance =
        TimeSpan.FromMicroseconds(1);

    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task GetProducts_Default_ReturnsOnlyActiveProducts()
    {
        ProductResponse activeProduct =
            await CreateProductAsync(
                CreateRequest(
                    sku: CreateUniqueSku("ACTIVE"),
                    isActive: true));

        ProductResponse inactiveProduct =
            await CreateProductAsync(
                CreateRequest(
                    sku: CreateUniqueSku("INACTIVE"),
                    isActive: false));

        using HttpResponseMessage response =
            await _client.GetAsync(ProductsEndpoint);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        ProductResponse[] products =
            await ReadRequiredAsync<ProductResponse[]>(
                response);

        Assert.Contains(
            products,
            product => product.Id == activeProduct.Id);

        Assert.DoesNotContain(
            products,
            product => product.Id == inactiveProduct.Id);

        Assert.All(
            products,
            product => Assert.True(product.IsActive));
    }

    [Fact]
    public async Task GetProducts_IncludeInactive_ReturnsAllProducts()
    {
        ProductResponse activeProduct =
            await CreateProductAsync(
                CreateRequest(
                    sku: CreateUniqueSku("ACTIVE"),
                    isActive: true));

        ProductResponse inactiveProduct =
            await CreateProductAsync(
                CreateRequest(
                    sku: CreateUniqueSku("INACTIVE"),
                    isActive: false));

        using HttpResponseMessage response =
            await _client.GetAsync(
                $"{ProductsEndpoint}?includeInactive=true");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        ProductResponse[] products =
            await ReadRequiredAsync<ProductResponse[]>(
                response);

        Assert.Contains(
            products,
            product => product.Id == activeProduct.Id);

        Assert.Contains(
            products,
            product => product.Id == inactiveProduct.Id);
    }

    [Fact]
    public async Task GetProductById_UnknownProduct_ReturnsNotFound()
    {
        Guid unknownProductId = Guid.NewGuid();

        using HttpResponseMessage response =
            await _client.GetAsync(
                $"{ProductsEndpoint}/{unknownProductId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_ValidRequest_PersistsNormalizedProduct()
    {
        CreateProductRequest request = new()
        {
            Name = "  Mechanical Keyboard  ",
            Sku = $"  {CreateUniqueSku("KEYBOARD").ToLowerInvariant()}  ",
            Description = "  Gaming keyboard  ",
            Category = "  Peripherals  ",
            PriceAmount = 2500m,
            Currency = "czk",
            IsActive = true,
        };

        using HttpResponseMessage createResponse =
            await _client.PostAsJsonAsync(
                ProductsEndpoint,
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        ProductResponse createdProduct =
            await ReadRequiredAsync<ProductResponse>(
                createResponse);

        Assert.NotEqual(
            Guid.Empty,
            createdProduct.Id);

        Assert.Equal(
            "Mechanical Keyboard",
            createdProduct.Name);

        Assert.Equal(
            request.Sku.Trim().ToUpperInvariant(),
            createdProduct.Sku);

        Assert.Equal(
            "Gaming keyboard",
            createdProduct.Description);

        Assert.Equal(
            "Peripherals",
            createdProduct.Category);

        Assert.Equal(
            2500m,
            createdProduct.PriceAmount);

        Assert.Equal(
            "CZK",
            createdProduct.Currency);

        Assert.True(createdProduct.IsActive);
        Assert.NotEqual(
            default,
            createdProduct.CreatedAtUtc);

        Assert.Null(createdProduct.UpdatedAtUtc);

        using HttpResponseMessage getResponse =
            await _client.GetAsync(
                $"{ProductsEndpoint}/{createdProduct.Id}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        ProductResponse persistedProduct =
            await ReadRequiredAsync<ProductResponse>(
                getResponse);

        AssertProductResponse(
            createdProduct,
            persistedProduct);
    }

    [Fact]
    public async Task CreateProduct_InvalidRequest_ReturnsBadRequest()
    {
        CreateProductRequest request = new()
        {
            Name = string.Empty,
            Sku = CreateUniqueSku("INVALID"),
            Description = "Invalid product",
            Category = "Tests",
            PriceAmount = 100m,
            Currency = "CZK",
            IsActive = true,
        };

        using HttpResponseMessage response =
            await _client.PostAsJsonAsync(
                ProductsEndpoint,
                request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_DuplicateSku_ReturnsConflict()
    {
        string sku = CreateUniqueSku("DUPLICATE");

        await CreateProductAsync(
            CreateRequest(sku));

        CreateProductRequest duplicateRequest =
            CreateRequest(
                sku.ToLowerInvariant());

        using HttpResponseMessage response =
            await _client.PostAsJsonAsync(
                ProductsEndpoint,
                duplicateRequest);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_ValidRequest_PersistsNewValues()
    {
        ProductResponse createdProduct =
            await CreateProductAsync(
                CreateRequest(
                    CreateUniqueSku("UPDATE-ORIGINAL")));

        UpdateProductRequest request = new()
        {
            Name = "  Updated keyboard  ",
            Sku = CreateUniqueSku("UPDATED").ToLowerInvariant(),
            Description = "  Updated description  ",
            Category = "  Updated category  ",
            PriceAmount = 3000m,
            Currency = "eur",
            IsActive = false,
        };

        using HttpResponseMessage updateResponse =
            await _client.PutAsJsonAsync(
                $"{ProductsEndpoint}/{createdProduct.Id}",
                request);

        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        ProductResponse updatedProduct =
            await ReadRequiredAsync<ProductResponse>(
                updateResponse);

        Assert.Equal(
            createdProduct.Id,
            updatedProduct.Id);

        Assert.Equal(
            "Updated keyboard",
            updatedProduct.Name);

        Assert.Equal(
            request.Sku.Trim().ToUpperInvariant(),
            updatedProduct.Sku);

        Assert.Equal(
            "Updated description",
            updatedProduct.Description);

        Assert.Equal(
            "Updated category",
            updatedProduct.Category);

        Assert.Equal(
            3000m,
            updatedProduct.PriceAmount);

        Assert.Equal(
            "EUR",
            updatedProduct.Currency);

        Assert.False(updatedProduct.IsActive);

        AssertTimestampEqual(
            createdProduct.CreatedAtUtc,
            updatedProduct.CreatedAtUtc);

        Assert.NotNull(updatedProduct.UpdatedAtUtc);

        using HttpResponseMessage getResponse =
            await _client.GetAsync(
                $"{ProductsEndpoint}/{createdProduct.Id}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        ProductResponse persistedProduct =
            await ReadRequiredAsync<ProductResponse>(
                getResponse);

        AssertProductResponse(
            updatedProduct,
            persistedProduct);
    }

    [Fact]
    public async Task UpdateProduct_DuplicateSku_ReturnsConflict()
    {
        ProductResponse firstProduct =
            await CreateProductAsync(
                CreateRequest(
                    CreateUniqueSku("FIRST")));

        ProductResponse secondProduct =
            await CreateProductAsync(
                CreateRequest(
                    CreateUniqueSku("SECOND")));

        UpdateProductRequest request = new()
        {
            Name = secondProduct.Name,
            Sku = firstProduct.Sku.ToLowerInvariant(),
            Description = secondProduct.Description,
            Category = secondProduct.Category,
            PriceAmount = secondProduct.PriceAmount,
            Currency = secondProduct.Currency,
            IsActive = secondProduct.IsActive,
        };

        using HttpResponseMessage response =
            await _client.PutAsJsonAsync(
                $"{ProductsEndpoint}/{secondProduct.Id}",
                request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_ExistingProduct_DeactivatesProduct()
    {
        ProductResponse createdProduct =
            await CreateProductAsync(
                CreateRequest(
                    CreateUniqueSku("DELETE")));

        using HttpResponseMessage deleteResponse =
            await _client.DeleteAsync(
                $"{ProductsEndpoint}/{createdProduct.Id}");

        Assert.Equal(
            HttpStatusCode.NoContent,
            deleteResponse.StatusCode);

        using HttpResponseMessage getResponse =
            await _client.GetAsync(
                $"{ProductsEndpoint}/{createdProduct.Id}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        ProductResponse deactivatedProduct =
            await ReadRequiredAsync<ProductResponse>(
                getResponse);

        Assert.Equal(
            createdProduct.Id,
            deactivatedProduct.Id);

        Assert.False(deactivatedProduct.IsActive);
        Assert.NotNull(deactivatedProduct.UpdatedAtUtc);

        using HttpResponseMessage listResponse =
            await _client.GetAsync(ProductsEndpoint);

        Assert.Equal(
            HttpStatusCode.OK,
            listResponse.StatusCode);

        ProductResponse[] activeProducts =
            await ReadRequiredAsync<ProductResponse[]>(
                listResponse);

        Assert.DoesNotContain(
            activeProducts,
            product => product.Id == createdProduct.Id);
    }

    [Fact]
    public async Task DeleteProduct_UnknownProduct_ReturnsNotFound()
    {
        Guid unknownProductId = Guid.NewGuid();

        using HttpResponseMessage response =
            await _client.DeleteAsync(
                $"{ProductsEndpoint}/{unknownProductId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    private async Task<ProductResponse> CreateProductAsync(
        CreateProductRequest request)
    {
        using HttpResponseMessage response =
            await _client.PostAsJsonAsync(
                ProductsEndpoint,
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        return await ReadRequiredAsync<ProductResponse>(
            response);
    }

    private static CreateProductRequest CreateRequest(
        string sku,
        bool isActive = true)
    {
        return new CreateProductRequest
        {
            Name = "Test product",
            Sku = sku,
            Description = "Integration test product",
            Category = "Tests",
            PriceAmount = 1000m,
            Currency = "CZK",
            IsActive = isActive,
        };
    }

    private static string CreateUniqueSku(
        string prefix)
    {
        string suffix = Guid.NewGuid()
            .ToString("N")[..8]
            .ToUpperInvariant();

        return $"{prefix}-{suffix}";
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response)
    {
        T? value =
            await response.Content.ReadFromJsonAsync<T>();

        return Assert.IsType<T>(value);
    }

    private static void AssertProductResponse(
        ProductResponse expected,
        ProductResponse actual)
    {
        Assert.Equal(
            expected.Id,
            actual.Id);

        Assert.Equal(
            expected.Name,
            actual.Name);

        Assert.Equal(
            expected.Sku,
            actual.Sku);

        Assert.Equal(
            expected.Description,
            actual.Description);

        Assert.Equal(
            expected.Category,
            actual.Category);

        Assert.Equal(
            expected.PriceAmount,
            actual.PriceAmount);

        Assert.Equal(
            expected.Currency,
            actual.Currency);

        Assert.Equal(
            expected.IsActive,
            actual.IsActive);

        AssertTimestampEqual(
            expected.CreatedAtUtc,
            actual.CreatedAtUtc);

        AssertNullableTimestampEqual(
            expected.UpdatedAtUtc,
            actual.UpdatedAtUtc);
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

    private static void AssertNullableTimestampEqual(
        DateTimeOffset? expected,
        DateTimeOffset? actual)
    {
        if (expected is null ||
            actual is null)
        {
            Assert.Equal(
                expected,
                actual);

            return;
        }

        AssertTimestampEqual(
            expected.Value,
            actual.Value);
    }
}
