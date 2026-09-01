using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using CatalogService.Contracts;
using CatalogService.Data;
using CatalogService.IntegrationTests.Infrastructure;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatalogService.IntegrationTests;

public sealed class CatalogServiceIntegrationTests(
    CatalogServiceFixture fixture)
    : IClassFixture<CatalogServiceFixture>,
      IAsyncLifetime
{
    private const string ProductsEndpoint =
        "/api/v1/products";

    private static readonly TimeSpan TimestampTolerance =
        TimeSpan.FromMicroseconds(1);

    private readonly HttpClient _client = fixture.Client;

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
        ReadinessTracksPostgreSqlOutageAndRecoveryWhileLivenessStaysHealthy()
    {
        CancellationToken cancellationToken =
            Xunit.TestContext.Current.CancellationToken;

        await AssertEndpointStatusAsync(
            "/live",
            HttpStatusCode.OK,
            cancellationToken);

        await AssertEndpointStatusAsync(
            "/ready",
            HttpStatusCode.OK,
            cancellationToken);

        await AssertEndpointStatusAsync(
            "/health",
            HttpStatusCode.OK,
            cancellationToken);

        await fixture.PausePostgresAsync(
            cancellationToken);

        try
        {
            await AssertEndpointStatusAsync(
                "/live",
                HttpStatusCode.OK,
                cancellationToken);

            using HttpResponseMessage unavailableResponse =
                await WaitForStatusAsync(
                    "/ready",
                    HttpStatusCode.ServiceUnavailable,
                    cancellationToken);

            string responseBody =
                await unavailableResponse.Content.ReadAsStringAsync(
                    cancellationToken);

            using JsonDocument unhealthyDocument =
                JsonDocument.Parse(
                    responseBody);

            JsonElement unhealthyRoot =
                unhealthyDocument.RootElement;

            Assert.Equal(
                "Unhealthy",
                unhealthyRoot
                    .GetProperty("status")
                    .GetString());

            JsonElement checks =
                unhealthyRoot.GetProperty(
                    "checks");

            JsonElement postgresqlCheck =
                checks
                    .EnumerateArray()
                    .Single(
                        check =>
                            string.Equals(
                                check
                                    .GetProperty("name")
                                    .GetString(),
                                "postgresql",
                                StringComparison.OrdinalIgnoreCase));

            Assert.Equal(
                "Unhealthy",
                postgresqlCheck
                    .GetProperty("status")
                    .GetString());

            Assert.DoesNotContain(
                fixture.PostgresConnectionString,
                responseBody,
                StringComparison.OrdinalIgnoreCase);

            await AssertEndpointStatusAsync(
                "/health",
                HttpStatusCode.ServiceUnavailable,
                cancellationToken);
        }
        finally
        {
            await fixture.UnpausePostgresAsync(
                cancellationToken);
        }

        using HttpResponseMessage recoveredResponse =
            await WaitForStatusAsync(
                "/ready",
                HttpStatusCode.OK,
                cancellationToken);

        string recoveredBody =
            await recoveredResponse.Content.ReadAsStringAsync(
                cancellationToken);

        using JsonDocument recoveredDocument =
            JsonDocument.Parse(
                recoveredBody);

        Assert.Equal(
            "Healthy",
            recoveredDocument.RootElement
                .GetProperty("status")
                .GetString());
    }

    public static IEnumerable<object?[]>
        CatalogMutationBoundaryCases()
    {
        string[] methods =
        [
            HttpMethod.Post.Method,
            HttpMethod.Put.Method,
            HttpMethod.Delete.Method
        ];

        foreach (string method in methods)
        {
            yield return
            [
                method,
                null,
                HttpStatusCode.Unauthorized
            ];

            yield return
            [
                method,
                EshopRoles.Customer,
                HttpStatusCode.Forbidden
            ];

            yield return
            [
                method,
                EshopRoles.Support,
                HttpStatusCode.Forbidden
            ];
        }
    }

    [Theory]
    [MemberData(nameof(CatalogMutationBoundaryCases))]
    public async Task
        CatalogMutationBoundaryRejectsUnauthorizedCallersWithoutPersistence(
            string method,
            string? role,
            HttpStatusCode expectedStatus)
    {
        ProductResponse existingProduct =
            await CreateProductAsync(
                CreateRequest(
                    CreateUniqueSku("BOUNDARY")));

        int productCountBefore =
            await GetProductCountAsync();

        using HttpRequestMessage request =
            CreateMutationBoundaryRequest(
                method,
                role,
                existingProduct);

        using HttpResponseMessage response =
            await _client.SendAsync(
                request,
                Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            expectedStatus,
            response.StatusCode);

        Assert.Equal(
            productCountBefore,
            await GetProductCountAsync());

        using HttpResponseMessage getResponse =
            await _client.GetAsync(
                $"{ProductsEndpoint}/{existingProduct.Id}",
                Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        ProductResponse persistedProduct =
            await ReadRequiredAsync<ProductResponse>(
                getResponse);

        AssertProductResponse(
            existingProduct,
            persistedProduct);
    }

    [Fact]
    public async Task GetProductsDefaultReturnsOnlyActiveProducts()
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
            await _client.GetAsync(ProductsEndpoint, Xunit.TestContext.Current.CancellationToken);

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
    public async Task GetProductsIncludeInactiveReturnsAllProducts()
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
                $"{ProductsEndpoint}?includeInactive=true", Xunit.TestContext.Current.CancellationToken);

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
    public async Task GetProductByIdUnknownProductReturnsNotFound()
    {
        Guid unknownProductId = Guid.NewGuid();

        using HttpResponseMessage response =
            await _client.GetAsync(
                $"{ProductsEndpoint}/{unknownProductId}", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateProductValidRequestPersistsNormalizedProduct()
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
            await SendAdminJsonAsync(
                HttpMethod.Post,
                ProductsEndpoint,
                request, Xunit.TestContext.Current.CancellationToken);

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
                $"{ProductsEndpoint}/{createdProduct.Id}", Xunit.TestContext.Current.CancellationToken);

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
    public async Task CreateProductInvalidRequestReturnsBadRequest()
    {
        int productCountBefore =
            await GetProductCountAsync();

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
            await SendAdminJsonAsync(
                HttpMethod.Post,
                ProductsEndpoint,
                request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        ValidationProblemDetails problem =
            await ReadRequiredAsync<ValidationProblemDetails>(
                response);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            problem.Status);

        Assert.Equal(
            "https://httpstatuses.com/400",
            problem.Type);

        Assert.Equal(
            "Request validation failed.",
            problem.Title);

        Assert.Equal(
            "One or more validation errors occurred.",
            problem.Detail);

        Assert.Equal(
            ProductsEndpoint,
            problem.Instance);

        Assert.Contains(
            "Name",
            problem.Errors.Keys);

        Assert.Equal(
            "model_validation_failed",
            GetRequiredStringExtension(
                problem,
                "errorCode"));

        Assert.False(
            string.IsNullOrWhiteSpace(
                GetRequiredStringExtension(
                    problem,
                    "traceId")));

        Assert.False(
            string.IsNullOrWhiteSpace(
                GetRequiredStringExtension(
                    problem,
                    "requestId")));

        Assert.Equal(
            productCountBefore,
            await GetProductCountAsync());
    }

    [Fact]
    public async Task CreateProductDuplicateSkuReturnsConflict()
    {
        string sku = CreateUniqueSku("DUPLICATE");

        await CreateProductAsync(
            CreateRequest(sku));

        CreateProductRequest duplicateRequest =
            CreateRequest(
                sku.ToLowerInvariant());

        using HttpResponseMessage response =
            await SendAdminJsonAsync(
                HttpMethod.Post,
                ProductsEndpoint,
                duplicateRequest, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateProductValidRequestPersistsNewValues()
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
            await SendAdminJsonAsync(
                HttpMethod.Put,
                $"{ProductsEndpoint}/{createdProduct.Id}",
                request, Xunit.TestContext.Current.CancellationToken);

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
                $"{ProductsEndpoint}/{createdProduct.Id}", Xunit.TestContext.Current.CancellationToken);

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
    public async Task UpdateProductDuplicateSkuReturnsConflict()
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
            await SendAdminJsonAsync(
                HttpMethod.Put,
                $"{ProductsEndpoint}/{secondProduct.Id}",
                request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    [Fact]
    public async Task DeleteProductExistingProductDeactivatesProduct()
    {
        ProductResponse createdProduct =
            await CreateProductAsync(
                CreateRequest(
                    CreateUniqueSku("DELETE")));

        using HttpResponseMessage deleteResponse =
            await SendAdminAsync(
                HttpMethod.Delete,
                $"{ProductsEndpoint}/{createdProduct.Id}",
                Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.NoContent,
            deleteResponse.StatusCode);

        using HttpResponseMessage getResponse =
            await _client.GetAsync(
                $"{ProductsEndpoint}/{createdProduct.Id}", Xunit.TestContext.Current.CancellationToken);

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
            await _client.GetAsync(ProductsEndpoint, Xunit.TestContext.Current.CancellationToken);

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
    public async Task DeleteProductUnknownProductReturnsNotFound()
    {
        Guid unknownProductId = Guid.NewGuid();

        using HttpResponseMessage response =
            await SendAdminAsync(
                HttpMethod.Delete,
                $"{ProductsEndpoint}/{unknownProductId}",
                Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    private async Task<ProductResponse> CreateProductAsync(
        CreateProductRequest request)
    {
        using HttpResponseMessage response =
            await SendAdminJsonAsync(
                HttpMethod.Post,
                ProductsEndpoint,
                request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        return await ReadRequiredAsync<ProductResponse>(
            response);
    }

    private static HttpRequestMessage
        CreateMutationBoundaryRequest(
            string method,
            string? role,
            ProductResponse existingProduct)
    {
        HttpRequestMessage request =
            method switch
            {
                "POST" => new HttpRequestMessage(
                    HttpMethod.Post,
                    ProductsEndpoint)
                {
                    Content = JsonContent.Create(
                        CreateRequest(
                            CreateUniqueSku(
                                "DENIED-CREATE")))
                },

                "PUT" => new HttpRequestMessage(
                    HttpMethod.Put,
                    $"{ProductsEndpoint}/{existingProduct.Id}")
                {
                    Content = JsonContent.Create(
                        new UpdateProductRequest
                        {
                            Name = "Denied update",
                            Sku = CreateUniqueSku(
                                "DENIED-UPDATE"),
                            Description =
                                "This update must not persist.",
                            Category = "Security tests",
                            PriceAmount = 9999m,
                            Currency = "EUR",
                            IsActive = false
                        })
                },

                "DELETE" => new HttpRequestMessage(
                    HttpMethod.Delete,
                    $"{ProductsEndpoint}/{existingProduct.Id}"),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(method),
                    method,
                    "Unsupported mutation method.")
            };

        if (role is not null)
        {
            AddAuthenticationHeaders(
                request,
                $"boundary-{role}-{Guid.NewGuid():N}",
                role);
        }

        return request;
    }

    private async Task<HttpResponseMessage>
        SendAdminAsync(
            HttpMethod method,
            string path,
            CancellationToken cancellationToken)
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                method,
                path,
                EshopRoles.Admin);

        return await _client.SendAsync(
            request,
            cancellationToken);
    }

    private async Task<HttpResponseMessage>
        SendAdminJsonAsync<TBody>(
            HttpMethod method,
            string path,
            TBody body,
            CancellationToken cancellationToken)
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                method,
                path,
                EshopRoles.Admin);

        request.Content =
            JsonContent.Create(body);

        return await _client.SendAsync(
            request,
            cancellationToken);
    }

    private static HttpRequestMessage
        CreateAuthenticatedRequest(
            HttpMethod method,
            string path,
            params string[] roles)
    {
        HttpRequestMessage request =
            new(method, path);

        AddAuthenticationHeaders(
            request,
            $"catalog-{Guid.NewGuid():N}",
            roles);

        return request;
    }

    private static void AddAuthenticationHeaders(
        HttpRequestMessage request,
        string subject,
        params string[] roles)
    {
        request.Headers.Add(
            TestAuthenticationHandler.SubjectHeaderName,
            subject);

        request.Headers.Add(
            TestAuthenticationHandler.RolesHeaderName,
            string.Join(',', roles));
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
            await response.Content.ReadFromJsonAsync<T>(Xunit.TestContext.Current.CancellationToken);

        return Assert.IsType<T>(value);
    }

    private async Task<int> GetProductCountAsync()
    {
        await using AsyncServiceScope scope =
            fixture.Factory.Services
                .CreateAsyncScope();

        CatalogDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<CatalogDbContext>();

        return await dbContext.Products
            .CountAsync(
                Xunit.TestContext.Current.CancellationToken);
    }

    private static string GetRequiredStringExtension(
        ProblemDetails problem,
        string key)
    {
        JsonElement value = Assert.IsType<JsonElement>(
            problem.Extensions[key]);

        return Assert.IsType<string>(
            value.GetString());
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

    private async Task AssertEndpointStatusAsync(
        string endpoint,
        HttpStatusCode expectedStatus,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                endpoint,
                cancellationToken);

        Assert.Equal(
            expectedStatus,
            response.StatusCode);
    }

    private async Task<HttpResponseMessage> WaitForStatusAsync(
        string endpoint,
        HttpStatusCode expectedStatus,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        while (true)
        {
            HttpResponseMessage response =
                await _client.GetAsync(
                    endpoint,
                    timeout.Token);

            if (response.StatusCode == expectedStatus)
            {
                return response;
            }

            response.Dispose();

            await Task.Delay(
                TimeSpan.FromMilliseconds(250),
                timeout.Token);
        }
    }
}
