using System.Net;
using System.Net.Http.Json;
using CatalogService.Contracts;
using CatalogService.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CatalogService.IntegrationTests;

public sealed class CatalogServiceIntegrationTests(
    CatalogServiceFixture fixture)
    : IClassFixture<CatalogServiceFixture>,
      IAsyncLifetime
{
    private const string ProductsPath =
        "/api/v1/products";

    public ValueTask InitializeAsync()
    {
        return fixture.ResetDatabaseAsync();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task
        Health_AnonymousRequest_ReturnsOk()
    {
        using HttpResponseMessage response =
            await fixture.Client.GetAsync("/health");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task
        GetProducts_EmptyDatabase_ReturnsEmptyCollection()
    {
        using HttpResponseMessage response =
            await fixture.Client.GetAsync(ProductsPath);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        ProductResponse[]? products =
            await response.Content
                .ReadFromJsonAsync<ProductResponse[]>();

        Assert.NotNull(products);
        Assert.Empty(products);
    }

    [Fact]
    public async Task
        CreateProduct_ValidRequest_PersistsNormalizedProduct()
    {
        CreateProductRequest request = new()
        {
            Name = "  Mechanical Keyboard  ",
            Sku = "  keyboard-001  ",
            Description = "  Gaming keyboard  ",
            Category = "  Peripherals  ",
            PriceAmount = 2_500m,
            Currency = "czk",
            IsActive = true
        };

        using HttpResponseMessage createResponse =
            await fixture.Client.PostAsJsonAsync(
                ProductsPath,
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        ProductResponse? createdProduct =
            await createResponse.Content
                .ReadFromJsonAsync<ProductResponse>();

        Assert.NotNull(createdProduct);
        Assert.NotEqual(Guid.Empty, createdProduct.Id);

        Assert.Equal(
            "Mechanical Keyboard",
            createdProduct.Name);

        Assert.Equal(
            "KEYBOARD-001",
            createdProduct.Sku);

        Assert.Equal(
            "Gaming keyboard",
            createdProduct.Description);

        Assert.Equal(
            "Peripherals",
            createdProduct.Category);

        Assert.Equal(
            2_500m,
            createdProduct.PriceAmount);

        Assert.Equal(
            "CZK",
            createdProduct.Currency);

        Assert.True(createdProduct.IsActive);
        Assert.Null(createdProduct.UpdatedAtUtc);

        Assert.NotNull(
            createResponse.Headers.Location);

        Assert.EndsWith(
            $"/api/v1/products/{createdProduct.Id}",
            createResponse.Headers.Location.ToString(),
            StringComparison.Ordinal);

        using HttpResponseMessage getResponse =
            await fixture.Client.GetAsync(
                $"{ProductsPath}/{createdProduct.Id}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        ProductResponse? persistedProduct =
            await getResponse.Content
                .ReadFromJsonAsync<ProductResponse>();

        Assert.Equal(
            createdProduct,
            persistedProduct);
    }

    [Fact]
    public async Task
        GetProducts_DefaultQuery_ExcludesInactiveProducts()
    {
        ProductResponse activeProduct =
            await CreateProductAsync(
                name: "Active product",
                isActive: true);

        ProductResponse inactiveProduct =
            await CreateProductAsync(
                name: "Inactive product",
                isActive: false);

        using HttpResponseMessage defaultResponse =
            await fixture.Client.GetAsync(ProductsPath);

        ProductResponse[]? activeProducts =
            await defaultResponse.Content
                .ReadFromJsonAsync<ProductResponse[]>();

        Assert.Equal(
            HttpStatusCode.OK,
            defaultResponse.StatusCode);

        Assert.NotNull(activeProducts);

        Assert.Contains(
            activeProducts,
            product => product.Id == activeProduct.Id);

        Assert.DoesNotContain(
            activeProducts,
            product => product.Id == inactiveProduct.Id);

        using HttpResponseMessage allResponse =
            await fixture.Client.GetAsync(
                $"{ProductsPath}?includeInactive=true");

        ProductResponse[]? allProducts =
            await allResponse.Content
                .ReadFromJsonAsync<ProductResponse[]>();

        Assert.Equal(
            HttpStatusCode.OK,
            allResponse.StatusCode);

        Assert.NotNull(allProducts);

        Assert.Contains(
            allProducts,
            product => product.Id == activeProduct.Id);

        Assert.Contains(
            allProducts,
            product => product.Id == inactiveProduct.Id);
    }

    [Fact]
    public async Task
        CreateProduct_DuplicateNormalizedSku_ReturnsConflict()
    {
        await CreateProductAsync(
            sku: "DUPLICATE-SKU");

        CreateProductRequest duplicateRequest = new()
        {
            Name = "Duplicate product",
            Sku = "  duplicate-sku  ",
            Description = "Duplicate",
            Category = "Testing",
            PriceAmount = 100m,
            Currency = "CZK",
            IsActive = true
        };

        using HttpResponseMessage response =
            await fixture.Client.PostAsJsonAsync(
                ProductsPath,
                duplicateRequest);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        ProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);

        Assert.Equal(
            StatusCodes.Status409Conflict,
            problem.Status);

        Assert.Equal(
            "Product SKU already exists.",
            problem.Title);
    }

    [Fact]
    public async Task
        CreateProduct_InvalidRequest_ReturnsValidationProblem()
    {
        object invalidRequest = new
        {
            name = "",
            sku = "",
            description = "Invalid product",
            category = "",
            priceAmount = 0,
            currency = "CZ",
            isActive = true
        };

        using HttpResponseMessage response =
            await fixture.Client.PostAsJsonAsync(
                ProductsPath,
                invalidRequest);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);

        ValidationProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.NotNull(problem);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            problem.Status);

        Assert.NotEmpty(problem.Errors);
    }

    [Fact]
    public async Task
        UpdateProduct_ValidRequest_PersistsNewValues()
    {
        ProductResponse createdProduct =
            await CreateProductAsync();

        UpdateProductRequest request = new()
        {
            Name = "  Updated keyboard  ",
            Sku = "  updated-sku  ",
            Description = "  Updated description  ",
            Category = "  Updated category  ",
            PriceAmount = 3_000m,
            Currency = "eur",
            IsActive = false
        };

        using HttpResponseMessage updateResponse =
            await fixture.Client.PutAsJsonAsync(
                $"{ProductsPath}/{createdProduct.Id}",
                request);

        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        ProductResponse? updatedProduct =
            await updateResponse.Content
                .ReadFromJsonAsync<ProductResponse>();

        Assert.NotNull(updatedProduct);
        Assert.Equal(createdProduct.Id, updatedProduct.Id);
        Assert.Equal("Updated keyboard", updatedProduct.Name);
        Assert.Equal("UPDATED-SKU", updatedProduct.Sku);

        Assert.Equal(
            "Updated description",
            updatedProduct.Description);

        Assert.Equal(
            "Updated category",
            updatedProduct.Category);

        Assert.Equal(3_000m, updatedProduct.PriceAmount);
        Assert.Equal("EUR", updatedProduct.Currency);
        Assert.False(updatedProduct.IsActive);
        Assert.NotNull(updatedProduct.UpdatedAtUtc);

        using HttpResponseMessage getResponse =
            await fixture.Client.GetAsync(
                $"{ProductsPath}/{createdProduct.Id}");

        ProductResponse? persistedProduct =
            await getResponse.Content
                .ReadFromJsonAsync<ProductResponse>();

        Assert.NotNull(persistedProduct);

        Assert.Equal(
            updatedProduct.Id,
            persistedProduct.Id);

        Assert.Equal(
            updatedProduct.Name,
            persistedProduct.Name);

        Assert.Equal(
            updatedProduct.Sku,
            persistedProduct.Sku);

        Assert.Equal(
            updatedProduct.Description,
            persistedProduct.Description);

        Assert.Equal(
            updatedProduct.Category,
            persistedProduct.Category);

        Assert.Equal(
            updatedProduct.PriceAmount,
            persistedProduct.PriceAmount);

        Assert.Equal(
            updatedProduct.Currency,
            persistedProduct.Currency);

        Assert.Equal(
            updatedProduct.IsActive,
            persistedProduct.IsActive);

        AssertTimestampEqualWithinPostgresPrecision(
            updatedProduct.CreatedAtUtc,
            persistedProduct.CreatedAtUtc);

        Assert.NotNull(updatedProduct.UpdatedAtUtc);
        Assert.NotNull(persistedProduct.UpdatedAtUtc);

        AssertTimestampEqualWithinPostgresPrecision(
            updatedProduct.UpdatedAtUtc.Value,
            persistedProduct.UpdatedAtUtc.Value);
    }

    [Fact]
    public async Task
        UpdateProduct_DuplicateSku_ReturnsConflict()
    {
        ProductResponse firstProduct =
            await CreateProductAsync(
                sku: "FIRST-SKU");

        ProductResponse secondProduct =
            await CreateProductAsync(
                sku: "SECOND-SKU");

        UpdateProductRequest request = new()
        {
            Name = secondProduct.Name,
            Sku = firstProduct.Sku,
            Description = secondProduct.Description,
            Category = secondProduct.Category,
            PriceAmount = secondProduct.PriceAmount,
            Currency = secondProduct.Currency,
            IsActive = true
        };

        using HttpResponseMessage response =
            await fixture.Client.PutAsJsonAsync(
                $"{ProductsPath}/{secondProduct.Id}",
                request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        ProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);

        Assert.Equal(
            "Product SKU already exists.",
            problem.Title);
    }

    [Fact]
    public async Task
        DeleteProduct_ExistingProduct_DeactivatesProduct()
    {
        ProductResponse product =
            await CreateProductAsync(
                isActive: true);

        using HttpResponseMessage deleteResponse =
            await fixture.Client.DeleteAsync(
                $"{ProductsPath}/{product.Id}");

        Assert.Equal(
            HttpStatusCode.NoContent,
            deleteResponse.StatusCode);

        using HttpResponseMessage detailResponse =
            await fixture.Client.GetAsync(
                $"{ProductsPath}/{product.Id}");

        ProductResponse? deactivatedProduct =
            await detailResponse.Content
                .ReadFromJsonAsync<ProductResponse>();

        Assert.Equal(
            HttpStatusCode.OK,
            detailResponse.StatusCode);

        Assert.NotNull(deactivatedProduct);
        Assert.False(deactivatedProduct.IsActive);
        Assert.NotNull(deactivatedProduct.UpdatedAtUtc);

        using HttpResponseMessage defaultListResponse =
            await fixture.Client.GetAsync(ProductsPath);

        ProductResponse[]? defaultProducts =
            await defaultListResponse.Content
                .ReadFromJsonAsync<ProductResponse[]>();

        Assert.NotNull(defaultProducts);

        Assert.DoesNotContain(
            defaultProducts,
            candidate => candidate.Id == product.Id);

        using HttpResponseMessage completeListResponse =
            await fixture.Client.GetAsync(
                $"{ProductsPath}?includeInactive=true");

        ProductResponse[]? allProducts =
            await completeListResponse.Content
                .ReadFromJsonAsync<ProductResponse[]>();

        Assert.NotNull(allProducts);

        Assert.Contains(
            allProducts,
            candidate =>
                candidate.Id == product.Id
                && !candidate.IsActive);
    }

    [Fact]
    public async Task
        MissingProduct_OperationsReturnNotFound()
    {
        Guid missingProductId =
            Guid.NewGuid();

        using HttpResponseMessage getResponse =
            await fixture.Client.GetAsync(
                $"{ProductsPath}/{missingProductId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            getResponse.StatusCode);

        UpdateProductRequest updateRequest = new()
        {
            Name = "Missing product",
            Sku = "MISSING-SKU",
            Description = "Missing",
            Category = "Testing",
            PriceAmount = 100m,
            Currency = "CZK",
            IsActive = true
        };

        using HttpResponseMessage updateResponse =
            await fixture.Client.PutAsJsonAsync(
                $"{ProductsPath}/{missingProductId}",
                updateRequest);

        Assert.Equal(
            HttpStatusCode.NotFound,
            updateResponse.StatusCode);

        using HttpResponseMessage deleteResponse =
            await fixture.Client.DeleteAsync(
                $"{ProductsPath}/{missingProductId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            deleteResponse.StatusCode);
    }

    private async Task<ProductResponse>
        CreateProductAsync(
            string? name = null,
            string? sku = null,
            bool isActive = true)
    {
        CreateProductRequest request = new()
        {
            Name =
                name
                ?? $"Product {Guid.NewGuid():N}",

            Sku =
                sku
                ?? $"SKU-{Guid.NewGuid():N}",

            Description =
                "Integration test product",

            Category =
                "Integration tests",

            PriceAmount =
                1_000m,

            Currency =
                "CZK",

            IsActive =
                isActive
        };

        using HttpResponseMessage response =
            await fixture.Client.PostAsJsonAsync(
                ProductsPath,
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        ProductResponse? product =
            await response.Content
                .ReadFromJsonAsync<ProductResponse>();

        return Assert.IsType<ProductResponse>(product);
    }

    private static void AssertTimestampEqualWithinPostgresPrecision(
        DateTimeOffset expected,
        DateTimeOffset actual)
    {
        TimeSpan difference =
            (expected - actual).Duration();

        Assert.True(
            difference <= TimeSpan.FromMicroseconds(1),
            $"Expected timestamp '{expected:O}', " +
            $"actual timestamp '{actual:O}', " +
            $"difference '{difference.TotalMicroseconds}' μs.");
    }
}
